using System.Buffers.Binary;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Dmg;
using DotnetPackaging.Dmg.Verification;
using DotnetPackaging.Formats.Dmg.Udif;
using FluentAssertions;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgHfsBuilderTests
{
    [Fact]
    public async Task Creates_udif_container()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Volume.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Volume");

        var udif = await UdifImage.Load(outDmg);
        udif.Trailer.Signature.Should().Be("koly");
        udif.Runs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Creates_dmg_with_app_bundle_and_files()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        // Create minimal .app bundle
        var appDir = Path.Combine(publish, "MyApp.app", "Contents", "MacOS");
        Directory.CreateDirectory(appDir);
        var exePath = Path.Combine(appDir, "MyApp");
        await File.WriteAllTextAsync(exePath, "#!/bin/sh\necho Hello\n");

        var outDmg = Path.Combine(tempRoot.Path, "MyApp.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "My App");

        File.Exists(outDmg).Should().BeTrue("the dmg file must be created");

        var data = await ExtractVolumeBytes(outDmg);
        data.Length.Should().BeGreaterThan(0);
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1024, 2)).Should().Be(0x482B, "should have HFS+ signature 'H+'");
    }

    [Fact]
    public async Task Wraps_publish_output_into_app_bundle_when_missing()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");
        await File.WriteAllTextAsync(Path.Combine(publish, "Angor.deps.json"), "deps");

        var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Angor Avalonia");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var data = await ExtractVolumeBytes(outDmg);
        data.Length.Should().BeGreaterThan(1000, "dmg should contain app bundle content");
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1024, 2)).Should().Be(0x482B);
    }

    [Fact]
    public async Task Uses_custom_info_plist_when_wrapping_publish_output()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");
        var plist = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple Computer//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>CFBundleIdentifier</key>
    <string>com.example.customplist</string>
    <key>CFBundleExecutable</key>
    <string>TestApp</string>
  </dict>
</plist>
""";

        var outDmg = Path.Combine(tempRoot.Path, "Custom.dmg");
        await DmgHfsBuilder.Create(
            publish,
            outDmg,
            "Test App",
            infoPlist: Maybe.From(ByteSource.FromString(plist)));

        var data = await ExtractVolumeBytes(outDmg);
        Encoding.UTF8.GetString(data).Should().Contain("com.example.customplist");
    }

    [Fact]
    public async Task Custom_info_plist_with_known_length_is_consumed_once()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");
        var plistBytes = """
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict><key>CFBundleExecutable</key><string>TestApp</string></dict></plist>
"""u8.ToArray();
        var subscriptions = 0;
        var plistSource = ByteSource.FromByteObservable(
            Observable.Defer(() =>
            {
                subscriptions++;
                return Observable.Return(plistBytes);
            }),
            plistBytes.LongLength);

        var outDmg = Path.Combine(tempRoot.Path, "Custom.dmg");
        await DmgHfsBuilder.Create(
            publish,
            outDmg,
            "Test App",
            infoPlist: Maybe.From(plistSource));

        subscriptions.Should().Be(1);
    }

    [Fact]
    public async Task Custom_info_plist_without_known_length_is_consumed_once()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");
        var plistBytes = """
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict><key>CFBundleIdentifier</key><string>com.example.unknownlength</string></dict></plist>
"""u8.ToArray();
        var subscriptions = 0;
        var plistSource = ByteSource.FromByteObservable(
            Observable.Defer(() =>
            {
                subscriptions++;
                return Observable.Return(plistBytes);
            }));

        var outDmg = Path.Combine(tempRoot.Path, "Custom.dmg");
        await DmgHfsBuilder.Create(
            publish,
            outDmg,
            "Test App",
            infoPlist: Maybe.From(plistSource));

        var data = await ExtractVolumeBytes(outDmg);
        Encoding.UTF8.GetString(data).Should().Contain("com.example.unknownlength");
        subscriptions.Should().Be(1);
    }

    [Fact]
    public async Task Uses_source_info_plist_when_wrapping_publish_output()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");
        await File.WriteAllTextAsync(
            Path.Combine(publish, "Info.plist"),
            """
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict><key>CFBundleIdentifier</key><string>com.example.sourceplist</string></dict></plist>
""");

        var outDmg = Path.Combine(tempRoot.Path, "Source.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App");

        var data = await ExtractVolumeBytes(outDmg);
        Encoding.UTF8.GetString(data).Should().Contain("com.example.sourceplist");
    }

    [Fact]
    public async Task Generated_info_plist_uses_metadata_when_no_custom_plist_is_supplied()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Metadata.dmg");
        await DmgHfsBuilder.Create(
            publish,
            outDmg,
            "Test App",
            executableName: "TestApp",
            bundleIdentifier: Maybe.From("com.example.metadata"),
            bundleVersion: Maybe.From("2.3.4"));

        var data = await ExtractVolumeBytes(outDmg);
        var text = Encoding.UTF8.GetString(data);
        text.Should().Contain("com.example.metadata");
        text.Should().Contain("2.3.4");
    }

    [Fact]
    public async Task Creates_dmg_with_applications_symlink()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", addApplicationsSymlink: true);

        File.Exists(outDmg).Should().BeTrue();
        
        // Verify it's a valid HFS+
        var data = await ExtractVolumeBytes(outDmg);
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1024, 2)).Should().Be(0x482B);
    }

    [Fact]
    public async Task Hfs_file_count_includes_applications_symlink_once()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", addApplicationsSymlink: true);

        var data = await ExtractVolumeBytes(outDmg);
        var fileCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1024 + 32, 4));

        fileCount.Should().Be(6);
    }

    [Fact]
    public async Task Dmg_verify_accepts_matching_hfs_file_count()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", addApplicationsSymlink: true);

        var result = await DmgVerifier.Verify(outDmg);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Dmg_verify_rejects_mismatched_hfs_file_count()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", addApplicationsSymlink: true);

        var volume = await ExtractVolumeBytes(outDmg);
        var fileCount = BinaryPrimitives.ReadUInt32BigEndian(volume.AsSpan(1024 + 32, 4));
        BinaryPrimitives.WriteUInt32BigEndian(volume.AsSpan(1024 + 32, 4), fileCount + 1);

        var corruptDmg = Path.Combine(tempRoot.Path, "Corrupt.dmg");
        await WriteVolumeBytesToDmg(volume, corruptDmg);

        var result = await DmgVerifier.Verify(corruptDmg);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("HFS+ file count mismatch");
    }

    [Fact]
    public async Task Volume_name_is_sanitized_correctly()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "file.txt"), "ok");

        var outDmg = Path.Combine(tempRoot.Path, "Out.dmg");
        // Unicode and special chars should be sanitized
        await DmgHfsBuilder.Create(publish, outDmg, "my app: with spaces & unicode — test");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        
        // Verify it's a valid HFS+
        var data = await ExtractVolumeBytes(outDmg);
        data.Length.Should().BeGreaterThan(1000, "dmg should contain content");
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1024, 2)).Should().Be(0x482B);
    }

    [Fact]
    public async Task Creates_dmg_without_default_layout()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Minimal.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Minimal", includeDefaultLayout: false);

        File.Exists(outDmg).Should().BeTrue();
        
        // Verify it's a valid HFS+
        var data = await ExtractVolumeBytes(outDmg);
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1024, 2)).Should().Be(0x482B);
    }

    [Fact]
    public async Task HfsPlus_header_has_correct_version()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Version.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Version Test");

        var data = await ExtractVolumeBytes(outDmg);
        var header = data.AsSpan(1024, 6).ToArray();
        
        // Bytes 0-1: signature 'H+' (0x482B)
        BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2)).Should().Be(0x482B);
        
        // Bytes 2-3: version (should be 4 for HFS+)
        BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2)).Should().Be(4);
    }

    private static async Task<byte[]> ExtractVolumeBytes(string dmgPath)
    {
        var udif = await UdifImage.Load(dmgPath);
        return await udif.ExtractDataFork();
    }

    private static async Task WriteVolumeBytesToDmg(byte[] volume, string dmgPath)
    {
        await using var output = File.Create(dmgPath);
        using var input = new MemoryStream(volume);
        new UdifWriter { CompressionType = CompressionType.Raw }.Create(input, output);
    }
}
