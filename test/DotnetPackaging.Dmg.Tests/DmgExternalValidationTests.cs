using CSharpFunctionalExtensions;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgExternalValidationTests
{
    [Fact]
    public void Hdiutil_plist_parser_reads_attached_hfs_devices()
    {
        const string plist = """
                             <?xml version="1.0" encoding="UTF-8"?>
                             <plist version="1.0">
                             <dict>
                                 <key>system-entities</key>
                                 <array>
                                     <dict>
                                         <key>dev-entry</key>
                                         <string>/dev/disk5</string>
                                         <key>mount-point</key>
                                         <string>/Volumes/Test App</string>
                                         <key>content-hint</key>
                                         <string>Apple_HFS</string>
                                     </dict>
                                 </array>
                             </dict>
                             </plist>
                             """;

        var devices = ExternalDmgValidationTools.ParseHdiutilDevices(plist);

        devices.Should().ContainSingle().Which.Should().Be(
            new HdiutilDevice("/dev/disk5", "/Volumes/Test App", "Apple_HFS"));
    }

    [SkippableFact]
    public async Task Default_packager_dmg_validates_with_external_hfs_tool_when_available()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "TestApp.dmg");
        var container = new DirectoryContainer(new System.IO.Abstractions.FileSystem().DirectoryInfo.New(publish)).AsRoot();
        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("Test App"),
            ExecutableName = Maybe.From("TestApp")
        };

        var result = await new DmgPackager().Pack(container, metadata);
        result.IsSuccess.Should().BeTrue();
        await result.Value.WriteTo(outDmg);

        var hdiutil = ExternalDmgValidationTools.FindHdiutil();
        if (hdiutil != null)
        {
            await ValidateWithHdiutil(hdiutil, outDmg, tempRoot.Path);
            return;
        }

        var dmg2img = ExternalDmgValidationTools.FindDmg2Img();
        var fsck = ExternalDmgValidationTools.FindHfsFsck();
        Skip.If(dmg2img == null || fsck == null, "Requires hdiutil, or both dmg2img and fsck.hfsplus/fsck_hfs.");

        var outImg = Path.Combine(tempRoot.Path, "TestApp.img");
        var convert = await ExternalDmgValidationTools.Run(dmg2img, outDmg, outImg);
        convert.ExitCode.Should().Be(0, convert.StdOut + convert.StdErr);

        var check = await ExternalDmgValidationTools.Run(fsck, "-n", outImg);
        check.ExitCode.Should().Be(0, check.StdOut + check.StdErr);
    }

    private static async Task ValidateWithHdiutil(string hdiutil, string dmgPath, string tempRoot)
    {
        var verify = await ExternalDmgValidationTools.Run(hdiutil, "verify", dmgPath);
        verify.ExitCode.Should().Be(0, verify.StdOut + verify.StdErr);

        await ValidateMountability(hdiutil, dmgPath, tempRoot);
        await ValidateHfsVolume(hdiutil, dmgPath);
    }

    private static async Task ValidateMountability(string hdiutil, string dmgPath, string tempRoot)
    {
        var mountPath = Path.Combine(tempRoot, "mount");
        Directory.CreateDirectory(mountPath);

        IReadOnlyList<HdiutilDevice> devices = [];
        try
        {
            var attach = await ExternalDmgValidationTools.Run(
                hdiutil,
                "attach",
                "-readonly",
                "-nobrowse",
                "-plist",
                "-mountpoint",
                mountPath,
                dmgPath);
            attach.ExitCode.Should().Be(0, attach.StdOut + attach.StdErr);

            devices = ExternalDmgValidationTools.ParseHdiutilDevices(attach.StdOut);
            var mountedDevice = devices.FirstOrDefault(device => !string.IsNullOrWhiteSpace(device.MountPoint));
            mountedDevice.Should().NotBeNull(attach.StdOut + attach.StdErr);
            Directory.Exists(mountedDevice!.MountPoint).Should().BeTrue(attach.StdOut + attach.StdErr);
        }
        finally
        {
            await DetachDevices(hdiutil, devices);
        }
    }

    private static async Task ValidateHfsVolume(string hdiutil, string dmgPath)
    {
        var fsck = ExternalDmgValidationTools.FindHfsFsck();
        Skip.If(fsck == null, "Requires fsck_hfs for mounted DMG HFS+ validation.");

        IReadOnlyList<HdiutilDevice> devices = [];
        try
        {
            var attach = await ExternalDmgValidationTools.Run(hdiutil, "attach", "-readonly", "-nomount", "-plist", dmgPath);
            attach.ExitCode.Should().Be(0, attach.StdOut + attach.StdErr);

            devices = ExternalDmgValidationTools.ParseHdiutilDevices(attach.StdOut);
            var hfsDevice = devices.FirstOrDefault(device =>
                device.ContentHint?.Contains("HFS", StringComparison.OrdinalIgnoreCase) == true);

            hfsDevice.Should().NotBeNull(attach.StdOut + attach.StdErr);
            var check = await ExternalDmgValidationTools.Run(fsck, "-n", hfsDevice!.Device!);
            check.ExitCode.Should().Be(0, check.StdOut + check.StdErr);
        }
        finally
        {
            await DetachDevices(hdiutil, devices);
        }
    }

    private static async Task DetachDevices(string hdiutil, IReadOnlyList<HdiutilDevice> devices)
    {
        var device = devices.FirstOrDefault()?.Device;
        if (!string.IsNullOrWhiteSpace(device))
        {
            await ExternalDmgValidationTools.Run(hdiutil, "detach", device, "-force");
        }
    }
}
