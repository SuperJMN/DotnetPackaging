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
    public async Task Generated_app_bundle_marks_only_main_executable_as_executable()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        var executable = Path.Combine(publish, "TestApp");
        var config = Path.Combine(publish, "TestApp.deps.json");
        var extensionlessHelper = Path.Combine(publish, "helper");
        var nestedDir = Path.Combine(publish, "tools");
        var nestedTool = Path.Combine(nestedDir, "nested-tool");
        Directory.CreateDirectory(nestedDir);
        await File.WriteAllTextAsync(executable, "exe");
        await File.WriteAllTextAsync(config, "deps");
        await File.WriteAllTextAsync(extensionlessHelper, "helper");
        await File.WriteAllTextAsync(nestedTool, "nested");
        SetUnixMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        SetUnixMode(config, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        SetUnixMode(extensionlessHelper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        SetUnixMode(nestedTool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", executableName: "TestApp");

        var modes = ReadCatalogFileModes(await ExtractVolumeBytes(outDmg));

        modes["TestApp"].Should().Be(0x81ED);
        modes["TestApp.deps.json"].Should().Be(0x81A4);
        modes["helper"].Should().Be(0x81A4);
        modes["nested-tool"].Should().Be(0x81A4);
        modes["Info.plist"].Should().Be(0x81A4);
    }

    [Fact]
    public async Task Existing_app_bundle_preserves_source_file_modes()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        var macOs = Path.Combine(publish, "MyApp.app", "Contents", "MacOS");
        var resources = Path.Combine(publish, "MyApp.app", "Contents", "Resources");
        Directory.CreateDirectory(macOs);
        Directory.CreateDirectory(resources);

        var executable = Path.Combine(macOs, "MyApp");
        var resource = Path.Combine(resources, "settings.json");
        await File.WriteAllTextAsync(executable, "exe");
        await File.WriteAllTextAsync(resource, "{}");
        SetUnixMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        SetUnixMode(resource, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var outDmg = Path.Combine(tempRoot.Path, "MyApp.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "My App");

        var modes = ReadCatalogFileModes(await ExtractVolumeBytes(outDmg));

        modes["MyApp"].Should().Be(ExpectedSourceMode(0x81ED));
        modes["settings.json"].Should().Be(ExpectedSourceMode(0x81A4));
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
    public async Task Preserves_custom_dmg_adornments_at_volume_root_when_wrapping_publish_output()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var customDsStore = Encoding.UTF8.GetBytes("custom ds store issue 180");
        var customVolumeIcon = Encoding.UTF8.GetBytes("custom volume icon issue 180");
        var customBackground = Encoding.UTF8.GetBytes("custom background issue 180");
        await File.WriteAllBytesAsync(Path.Combine(publish, ".DS_Store"), customDsStore);
        await File.WriteAllBytesAsync(Path.Combine(publish, ".VolumeIcon.icns"), customVolumeIcon);
        var background = Path.Combine(publish, ".background");
        Directory.CreateDirectory(background);
        await File.WriteAllBytesAsync(Path.Combine(background, "Custom.txt"), customBackground);

        var outDmg = Path.Combine(tempRoot.Path, "Custom.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Custom");

        var catalog = await ReadCatalog(outDmg);
        catalog.ReadFile(".DS_Store").Should().Equal(customDsStore);
        catalog.ReadFile(".VolumeIcon.icns").Should().Equal(customVolumeIcon);
        catalog.ReadFile(".background/Custom.txt").Should().Equal(customBackground);
        catalog.ReadFile(".background/Background.png").Should().NotBeNull("the default layout fills the missing background image");
    }

    [Fact]
    public async Task Preserves_custom_dmg_adornments_at_volume_root_with_existing_app_bundle()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(Path.Combine(publish, "MyApp.app", "Contents", "MacOS"));
        await File.WriteAllTextAsync(Path.Combine(publish, "MyApp.app", "Contents", "MacOS", "MyApp"), "exe");

        var customDsStore = Encoding.UTF8.GetBytes("app bundle ds store issue 180");
        var customVolumeIcon = Encoding.UTF8.GetBytes("app bundle volume icon issue 180");
        var customBackground = Encoding.UTF8.GetBytes("app bundle background issue 180");
        await File.WriteAllBytesAsync(Path.Combine(publish, ".DS_Store"), customDsStore);
        await File.WriteAllBytesAsync(Path.Combine(publish, ".VolumeIcon.icns"), customVolumeIcon);
        var background = Path.Combine(publish, ".background");
        Directory.CreateDirectory(background);
        await File.WriteAllBytesAsync(Path.Combine(background, "Custom.txt"), customBackground);

        var outDmg = Path.Combine(tempRoot.Path, "ExistingApp.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Existing App");

        var catalog = await ReadCatalog(outDmg);
        catalog.ReadFile(".DS_Store").Should().Equal(customDsStore);
        catalog.ReadFile(".VolumeIcon.icns").Should().Equal(customVolumeIcon);
        catalog.ReadFile(".background/Custom.txt").Should().Equal(customBackground);
    }

    [Fact]
    public async Task Preserves_custom_dmg_adornments_without_injecting_defaults_when_default_layout_is_disabled()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var customDsStore = Encoding.UTF8.GetBytes("no default ds store issue 180");
        var customBackground = Encoding.UTF8.GetBytes("no default background issue 180");
        await File.WriteAllBytesAsync(Path.Combine(publish, ".DS_Store"), customDsStore);
        var background = Path.Combine(publish, ".background");
        Directory.CreateDirectory(background);
        await File.WriteAllBytesAsync(Path.Combine(background, "Custom.txt"), customBackground);

        var outDmg = Path.Combine(tempRoot.Path, "NoDefault.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "No Default", includeDefaultLayout: false);

        var catalog = await ReadCatalog(outDmg);
        catalog.ReadFile(".DS_Store").Should().Equal(customDsStore);
        catalog.ReadFile(".background/Custom.txt").Should().Equal(customBackground);
        catalog.ReadFile(".background/Background.png").Should().BeNull("disabled default layout must not inject embedded assets");
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

    private static async Task<HfsCatalogSnapshot> ReadCatalog(string dmgPath)
    {
        var volumeBytes = await ExtractVolumeBytes(dmgPath);
        return HfsCatalogSnapshot.Read(volumeBytes);
    }

    private sealed class HfsCatalogSnapshot
    {
        private const int VolumeHeaderOffset = 1024;
        private const int CatalogForkOffset = 272;
        private const uint RootFolderId = 2;
        private readonly IReadOnlyList<HfsCatalogEntry> entries;

        private HfsCatalogSnapshot(IReadOnlyList<HfsCatalogEntry> entries)
        {
            this.entries = entries;
        }

        public static HfsCatalogSnapshot Read(byte[] volumeBytes)
        {
            var blockSize = BinaryPrimitives.ReadUInt32BigEndian(volumeBytes.AsSpan(VolumeHeaderOffset + 40, 4));
            var catalogStartBlock = BinaryPrimitives.ReadUInt32BigEndian(volumeBytes.AsSpan(VolumeHeaderOffset + CatalogForkOffset + 16, 4));
            var catalogBlockCount = BinaryPrimitives.ReadUInt32BigEndian(volumeBytes.AsSpan(VolumeHeaderOffset + CatalogForkOffset + 20, 4));
            var catalogOffset = checked((int)(catalogStartBlock * blockSize));
            var catalogLength = checked((int)(catalogBlockCount * blockSize));
            var catalogBytes = volumeBytes.AsSpan(catalogOffset, catalogLength);

            var nodeSize = BinaryPrimitives.ReadUInt16BigEndian(catalogBytes[32..34]);
            var totalNodes = BinaryPrimitives.ReadUInt32BigEndian(catalogBytes[36..40]);
            var entries = new List<HfsCatalogEntry>();

            for (var nodeId = 0; nodeId < totalNodes; nodeId++)
            {
                var node = catalogBytes.Slice(checked((int)(nodeId * nodeSize)), nodeSize);
                if (node[8] != 0xFF)
                {
                    continue;
                }

                var recordCount = BinaryPrimitives.ReadUInt16BigEndian(node[10..12]);
                for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
                {
                    var offset = BinaryPrimitives.ReadUInt16BigEndian(node.Slice(nodeSize - 2 - recordIndex * 2, 2));
                    var record = node[offset..];
                    var keyLength = BinaryPrimitives.ReadUInt16BigEndian(record[..2]);
                    var parentId = BinaryPrimitives.ReadUInt32BigEndian(record[2..6]);
                    var nameLength = BinaryPrimitives.ReadUInt16BigEndian(record[6..8]);
                    var name = Encoding.BigEndianUnicode.GetString(record.Slice(8, nameLength * 2));
                    var dataOffset = 2 + keyLength;
                    var recordType = BinaryPrimitives.ReadInt16BigEndian(record.Slice(dataOffset, 2));

                    switch (recordType)
                    {
                        case 1:
                            var folderId = BinaryPrimitives.ReadUInt32BigEndian(record.Slice(dataOffset + 8, 4));
                            entries.Add(HfsCatalogEntry.Directory(parentId, name, folderId));
                            break;
                        case 2:
                            var dataForkOffset = dataOffset + 88;
                            var logicalSize = BinaryPrimitives.ReadUInt64BigEndian(record.Slice(dataForkOffset, 8));
                            var startBlock = BinaryPrimitives.ReadUInt32BigEndian(record.Slice(dataForkOffset + 16, 4));
                            var contentOffset = checked((int)(startBlock * blockSize));
                            var content = volumeBytes.AsSpan(contentOffset, checked((int)logicalSize)).ToArray();
                            entries.Add(HfsCatalogEntry.File(parentId, name, content));
                            break;
                    }
                }
            }

            return new HfsCatalogSnapshot(entries);
        }

        public byte[]? ReadFile(string path)
        {
            var currentParentId = RootFolderId;
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < parts.Length; i++)
            {
                var isLast = i == parts.Length - 1;
                var entry = entries.SingleOrDefault(x => x.ParentId == currentParentId && x.Name == parts[i]);
                if (entry == null)
                {
                    return null;
                }

                if (isLast)
                {
                    return entry.Content;
                }

                if (entry.FolderId == null)
                {
                    return null;
                }

                currentParentId = entry.FolderId.Value;
            }

            return null;
        }
    }

    private sealed record HfsCatalogEntry(uint ParentId, string Name, uint? FolderId, byte[]? Content)
    {
        public static HfsCatalogEntry Directory(uint parentId, string name, uint folderId)
            => new(parentId, name, folderId, null);

        public static HfsCatalogEntry File(uint parentId, string name, byte[] content)
            => new(parentId, name, null, content);
    }

    private static void SetUnixMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    private static ushort ExpectedSourceMode(ushort unixMode)
    {
        return OperatingSystem.IsWindows() ? (ushort)0x81A4 : unixMode;
    }

    private static Dictionary<string, ushort> ReadCatalogFileModes(byte[] volume)
    {
        var blockSize = BinaryPrimitives.ReadUInt32BigEndian(volume.AsSpan(1024 + 40, 4));
        var catalogFork = volume.AsSpan(1024 + 272, 80);
        var catalogStartBlock = BinaryPrimitives.ReadUInt32BigEndian(catalogFork[16..20]);
        var catalogOffset = checked((int)(catalogStartBlock * blockSize));
        var headerRecord = volume.AsSpan(catalogOffset + 14, 106);
        var firstLeafNode = BinaryPrimitives.ReadUInt32BigEndian(headerRecord[10..14]);
        var nodeSize = BinaryPrimitives.ReadUInt16BigEndian(headerRecord[18..20]);
        var result = new Dictionary<string, ushort>(StringComparer.Ordinal);

        for (var nodeIndex = firstLeafNode; nodeIndex != 0;)
        {
            var node = volume.AsSpan(catalogOffset + checked((int)(nodeIndex * nodeSize)), nodeSize);
            var forwardLink = BinaryPrimitives.ReadUInt32BigEndian(node[0..4]);
            var recordCount = BinaryPrimitives.ReadUInt16BigEndian(node[10..12]);

            for (var i = 0; i < recordCount; i++)
            {
                var start = ReadNodeRecordOffset(node, nodeSize, i);
                var end = ReadNodeRecordOffset(node, nodeSize, i + 1);
                var record = node[start..end];
                var keyLength = BinaryPrimitives.ReadUInt16BigEndian(record[0..2]);
                var nameLength = BinaryPrimitives.ReadUInt16BigEndian(record[6..8]);
                var name = Encoding.BigEndianUnicode.GetString(record.Slice(8, nameLength * 2));
                var recordData = record[(2 + keyLength)..];
                var recordType = BinaryPrimitives.ReadInt16BigEndian(recordData[0..2]);

                if (recordType == 0x0002)
                {
                    result[name] = BinaryPrimitives.ReadUInt16BigEndian(recordData[42..44]);
                }
            }

            nodeIndex = forwardLink;
        }

        return result;
    }

    private static int ReadNodeRecordOffset(ReadOnlySpan<byte> node, int nodeSize, int recordIndex)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(node.Slice(nodeSize - 2 - recordIndex * 2, 2));
    }
}
