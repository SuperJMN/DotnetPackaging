using CSharpFunctionalExtensions;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgExternalValidationTests
{
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
            var verify = await ExternalDmgValidationTools.Run(hdiutil, "verify", outDmg);
            verify.ExitCode.Should().Be(0, verify.StdOut + verify.StdErr);
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
}
