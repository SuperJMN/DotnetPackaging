using System.Buffers.Binary;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Formats.Dmg.Udif;
using DotnetPackaging.Publish;
using FluentAssertions;
using Serilog.Core;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgPackagerTests
{
    [Fact]
    public async Task Pack_should_preserve_length_and_delete_temp_dmg_after_consumption()
    {
        using var tempRoot = new TempDir();
        var existingTempDmgs = SnapshotTempDmgs();
        var container = CreateContainer(ByteSource.FromBytes("exe"u8.ToArray()));
        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("Test App"),
            ExecutableName = Maybe.From("TestApp")
        };

        var result = await new DmgPackager().Pack(container, metadata);
        result.IsSuccess.Should().BeTrue();

        var output = Path.Combine(tempRoot.Path, "TestApp.dmg");
        var write = await result.Value.WriteTo(output);

        result.Value.Length.HasValue.Should().BeTrue();
        result.Value.Length.Value.Should().Be(new FileInfo(output).Length);
        write.IsSuccess.Should().BeTrue();
        NewTempDmgs(existingTempDmgs).Should().BeEmpty();
    }

    [Fact]
    public async Task Pack_should_not_leave_temp_dmg_when_container_write_fails()
    {
        var existingTempDmgs = SnapshotTempDmgs();
        var failingSource = ByteSource.FromByteObservable(Observable.Throw<byte[]>(new IOException("boom")));
        var container = CreateContainer(failingSource);

        var result = await new DmgPackager().Pack(container, new DmgPackagerMetadata());

        result.IsFailure.Should().BeTrue();
        NewTempDmgs(existingTempDmgs).Should().BeEmpty();
    }

    [Fact]
    public async Task Pack_should_embed_custom_info_plist()
    {
        using var tempRoot = new TempDir();
        var container = CreateContainer(ByteSource.FromBytes("exe"u8.ToArray()));
        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("Test App"),
            ExecutableName = Maybe.From("TestApp"),
            InfoPlist = Maybe.From(ByteSource.FromString("""
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict><key>CFBundleIdentifier</key><string>com.example.packagerplist</string></dict></plist>
"""))
        };

        var result = await new DmgPackager().Pack(container, metadata);
        result.IsSuccess.Should().BeTrue();

        var output = Path.Combine(tempRoot.Path, "TestApp.dmg");
        var write = await result.Value.WriteTo(output);

        write.IsSuccess.Should().BeTrue();
        var data = await ExtractVolumeBytes(output);
        Encoding.UTF8.GetString(data).Should().Contain("com.example.packagerplist");
    }

    [Fact]
    public async Task Pack_should_map_metadata_to_generated_info_plist()
    {
        using var tempRoot = new TempDir();
        var container = CreateContainer(ByteSource.FromBytes("exe"u8.ToArray()));
        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("Test App"),
            ExecutableName = Maybe.From("TestApp"),
            BundleIdentifier = Maybe.From("com.example.packagermetadata"),
            BundleVersion = Maybe.From("3.2.1"),
            Vendor = Maybe.From("Packager Vendor")
        };

        var result = await new DmgPackager().Pack(container, metadata);
        result.IsSuccess.Should().BeTrue();

        var output = Path.Combine(tempRoot.Path, "TestApp.dmg");
        var write = await result.Value.WriteTo(output);

        write.IsSuccess.Should().BeTrue();
        var text = Encoding.UTF8.GetString(await ExtractVolumeBytes(output));
        text.Should().Contain("com.example.packagermetadata");
        text.Should().Contain("3.2.1");
        text.Should().Contain("CFBundleGetInfoString");
        text.Should().Contain("Packager Vendor");
    }

    [Fact]
    public async Task Pack_directory_preserves_existing_app_source_file_modes()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        var macOs = Path.Combine(publish, "MyApp.app", "Contents", "MacOS");
        var resources = Path.Combine(publish, "MyApp.app", "Contents", "Resources");
        Directory.CreateDirectory(macOs);
        Directory.CreateDirectory(resources);

        var executable = Path.Combine(macOs, "MyApp");
        var settings = Path.Combine(resources, "settings.json");
        await File.WriteAllTextAsync(executable, "exe");
        await File.WriteAllTextAsync(settings, "{}");
        SetUnixMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        SetUnixMode(settings, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("My App"),
            IncludeDefaultLayout = Maybe.From(false),
            AddApplicationsSymlink = Maybe.From(false)
        };

        var result = await new DmgPackager().PackDirectory(new DirectoryInfo(publish), metadata);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);

        var output = Path.Combine(tempRoot.Path, "MyApp.dmg");
        var write = await result.Value.WriteTo(output);

        write.IsSuccess.Should().BeTrue();
        var modes = ReadCatalogFileModes(await ExtractVolumeBytes(output));
        modes["MyApp"].Should().Be(ExpectedSourceMode(0x81ED));
        modes["settings.json"].Should().Be(ExpectedSourceMode(0x8180));
    }

    [Fact]
    public async Task Pack_preserves_directory_backed_container_source_file_modes()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        var macOs = Path.Combine(publish, "MyApp.app", "Contents", "MacOS");
        var resources = Path.Combine(publish, "MyApp.app", "Contents", "Resources");
        Directory.CreateDirectory(macOs);
        Directory.CreateDirectory(resources);

        var executable = Path.Combine(macOs, "MyApp");
        var settings = Path.Combine(resources, "settings.json");
        await File.WriteAllTextAsync(executable, "exe");
        await File.WriteAllTextAsync(settings, "{}");
        SetUnixMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        SetUnixMode(settings, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        using var container = new DisposableDirectoryContainer(publish, Serilog.Core.Logger.None);
        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("My App"),
            IncludeDefaultLayout = Maybe.From(false),
            AddApplicationsSymlink = Maybe.From(false)
        };

        var result = await new DmgPackager().Pack(container, metadata);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);

        var output = Path.Combine(tempRoot.Path, "MyApp.dmg");
        var write = await result.Value.WriteTo(output);

        write.IsSuccess.Should().BeTrue();
        var modes = ReadCatalogFileModes(await ExtractVolumeBytes(output));
        modes["MyApp"].Should().Be(ExpectedSourceMode(0x81ED));
        modes["settings.json"].Should().Be(ExpectedSourceMode(0x81A4));
    }

    [Fact]
    public async Task Pack_directory_generated_app_bundle_keeps_license_and_notice_non_executable()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        var executable = Path.Combine(publish, "TestApp");
        await File.WriteAllTextAsync(executable, "exe");
        await File.WriteAllTextAsync(Path.Combine(publish, "LICENSE"), "license");
        await File.WriteAllTextAsync(Path.Combine(publish, "NOTICE"), "notice");
        SetUnixMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("Test App"),
            ExecutableName = Maybe.From("TestApp"),
            IncludeDefaultLayout = Maybe.From(false),
            AddApplicationsSymlink = Maybe.From(false)
        };

        var result = await new DmgPackager().PackDirectory(publish, metadata);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);

        var output = Path.Combine(tempRoot.Path, "TestApp.dmg");
        var write = await result.Value.WriteTo(output);

        write.IsSuccess.Should().BeTrue();
        var modes = ReadCatalogFileModes(await ExtractVolumeBytes(output));
        modes["TestApp"].Should().Be(0x81ED);
        modes["LICENSE"].Should().Be(0x81A4);
        modes["NOTICE"].Should().Be(0x81A4);
    }

    [Fact]
    public async Task From_published_project_should_map_project_company_to_generated_info_plist()
    {
        using var tempRoot = new TempDir();
        var projectPath = CreateProject(tempRoot.Path, "Project Vendor");
        var context = ProjectPackagingContext.FromProject(projectPath, Logger.None);
        context.IsSuccess.Should().BeTrue(context.IsFailure ? context.Error : "");

        var container = CreateContainer(ByteSource.FromBytes("exe"u8.ToArray()), "sample-runner");
        var source = new DmgPackager().FromPublishedProject(container, context.Value, logger: Logger.None);

        var output = Path.Combine(tempRoot.Path, "ProjectVendor.dmg");
        var write = await source.WriteTo(output);

        write.IsSuccess.Should().BeTrue(write.IsFailure ? write.Error : "");
        var text = Encoding.UTF8.GetString(await ExtractVolumeBytes(output));
        text.Should().Contain("Project Vendor");
    }

    [Fact]
    public async Task From_published_project_should_prefer_metadata_vendor_over_project_company()
    {
        using var tempRoot = new TempDir();
        var projectPath = CreateProject(tempRoot.Path, "Project Vendor");
        var context = ProjectPackagingContext.FromProject(projectPath, Logger.None);
        context.IsSuccess.Should().BeTrue(context.IsFailure ? context.Error : "");
        var metadata = new DmgPackagerMetadata { Vendor = Maybe.From("CLI Vendor") };

        var container = CreateContainer(ByteSource.FromBytes("exe"u8.ToArray()), "sample-runner");
        var source = new DmgPackager().FromPublishedProject(container, context.Value, metadata, Logger.None);

        var output = Path.Combine(tempRoot.Path, "CliVendor.dmg");
        var write = await source.WriteTo(output);

        write.IsSuccess.Should().BeTrue(write.IsFailure ? write.Error : "");
        var text = Encoding.UTF8.GetString(await ExtractVolumeBytes(output));
        text.Should().Contain("CLI Vendor");
        text.Should().NotContain("Project Vendor");
    }

    private static string CreateProject(string directory, string company)
    {
        var projectPath = Path.Combine(directory, "SampleApp.csproj");
        File.WriteAllText(projectPath, $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <AssemblyName>sample-runner</AssemblyName>
    <Product>Sample App</Product>
    <Company>{company}</Company>
    <PackageId>com.example.sample</PackageId>
    <Version>4.5.6</Version>
  </PropertyGroup>
</Project>
""");

        return projectPath;
    }

    private static IContainer CreateContainer(IByteSource source, string fileName = "TestApp")
    {
        return new RootContainer(
            new[] { new NamedByteSource(fileName, source) },
            Enumerable.Empty<INamedContainer>());
    }

    private static HashSet<string> SnapshotTempDmgs()
    {
        return Directory
            .EnumerateFiles(Path.GetTempPath(), "dp-dmg-*.dmg")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> NewTempDmgs(HashSet<string> existing)
    {
        return Directory
            .EnumerateFiles(Path.GetTempPath(), "dp-dmg-*.dmg")
            .Where(path => !existing.Contains(path));
    }

    private static async Task<byte[]> ExtractVolumeBytes(string dmgPath)
    {
        var udif = await UdifImage.Load(dmgPath);
        return await udif.ExtractDataFork();
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
