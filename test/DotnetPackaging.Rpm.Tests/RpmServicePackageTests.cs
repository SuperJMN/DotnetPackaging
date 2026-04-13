using CSharpFunctionalExtensions;
using DotnetPackaging;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using IOPath = System.IO.Path;

namespace DotnetPackaging.Rpm.Tests;

[Collection("rpm-service-package")]
public class RpmServicePackageTests
{
    private readonly RpmServicePackageFixture fixture;

    public RpmServicePackageTests(RpmServicePackageFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Header_contains_postin_scriptlet()
    {
        var header = fixture.Archive.Header;
        header.HasTag(RpmTestTags.PostIn).Should().BeTrue();
        var script = header.GetString(RpmTestTags.PostIn);
        script.Should().Contain("systemctl daemon-reload");
        script.Should().Contain("systemctl enable my-service.service");
        script.Should().Contain("systemctl start my-service.service");
    }

    [Fact]
    public void Header_contains_preun_scriptlet()
    {
        var header = fixture.Archive.Header;
        header.HasTag(RpmTestTags.PreUn).Should().BeTrue();
        var script = header.GetString(RpmTestTags.PreUn);
        script.Should().Contain("systemctl stop my-service.service");
        script.Should().Contain("systemctl disable my-service.service");
    }

    [Fact]
    public void Header_contains_postun_scriptlet()
    {
        var header = fixture.Archive.Header;
        header.HasTag(RpmTestTags.PostUn).Should().BeTrue();
        var script = header.GetString(RpmTestTags.PostUn);
        script.Should().Contain("systemctl daemon-reload");
    }

    [Fact]
    public void Scriptlet_interpreters_are_bin_sh()
    {
        var header = fixture.Archive.Header;
        header.GetString(RpmTestTags.PostInProg).Should().Be("/bin/sh");
        header.GetString(RpmTestTags.PreUnProg).Should().Be("/bin/sh");
        header.GetString(RpmTestTags.PostUnProg).Should().Be("/bin/sh");
    }

    [Fact]
    public void Payload_contains_systemd_unit_file()
    {
        fixture.PayloadEntries.Should().NotBeEmpty("CPIO payload must be parseable");
        var entries = fixture.PayloadEntries;
        entries.Select(e => e.Name).Should().Contain("usr/lib/systemd/system/my-service.service");
    }

    [Fact]
    public void Systemd_unit_file_has_correct_content()
    {
        fixture.PayloadEntries.Should().NotBeEmpty("CPIO payload must be parseable");
        var unitFile = fixture.PayloadEntries.Single(e => e.Name == "usr/lib/systemd/system/my-service.service");
        var text = Encoding.ASCII.GetString(unitFile.Data);

        text.Should().Contain("[Unit]");
        text.Should().Contain("[Service]");
        text.Should().Contain("[Install]");
        text.Should().Contain("Type=simple");
        text.Should().Contain("ExecStart=/opt/my-service/my-service");
        text.Should().Contain("WorkingDirectory=/opt/my-service");
        text.Should().Contain("Restart=on-failure");
        text.Should().Contain("WantedBy=multi-user.target");
        text.Should().Contain("SyslogIdentifier=my-service");
    }

    [Fact]
    public void Payload_does_not_contain_desktop_file()
    {
        fixture.PayloadEntries.Should().NotBeEmpty("CPIO payload must be parseable");
        var entries = fixture.PayloadEntries;
        entries.Select(e => e.Name).Should().NotContain(n => n.Contains(".desktop"));
    }

    [Fact]
    public void Payload_still_contains_usr_bin_wrapper()
    {
        fixture.PayloadEntries.Should().NotBeEmpty("CPIO payload must be parseable");
        var entries = fixture.PayloadEntries;
        entries.Select(e => e.Name).Should().Contain("usr/bin/my-service");

        var wrapper = entries.Single(e => e.Name == "usr/bin/my-service");
        var text = Encoding.ASCII.GetString(wrapper.Data);
        text.Should().StartWith("#!/usr/bin/env sh");
        text.Should().Contain("/opt/my-service/my-service");
    }

    [Fact]
    public void Payload_contains_application_files()
    {
        fixture.PayloadEntries.Should().NotBeEmpty("CPIO payload must be parseable");
        var entries = fixture.PayloadEntries;
        entries.Select(e => e.Name).Should().Contain("opt/my-service/my-service");
        entries.Select(e => e.Name).Should().Contain("opt/my-service/appsettings.json");
    }

    [Fact]
    public void File_list_in_header_contains_service_unit_path()
    {
        var header = fixture.Archive.Header;
        var baseNames = header.GetStringArray(RpmTestTags.BaseNames);
        var dirNames = header.GetStringArray(RpmTestTags.DirNames);
        var dirIndexes = header.GetInt32Array(RpmTestTags.DirIndexes);
        var paths = new string[baseNames.Length];
        for (var i = 0; i < baseNames.Length; i++)
        {
            paths[i] = $"{dirNames[dirIndexes[i]]}{baseNames[i]}";
        }

        paths.Should().Contain("/usr/lib/systemd/system/my-service.service");
    }

    [Fact]
    public void File_list_in_header_does_not_contain_desktop_file()
    {
        var header = fixture.Archive.Header;
        var baseNames = header.GetStringArray(RpmTestTags.BaseNames);

        baseNames.Should().NotContain(n => n.Contains(".desktop"));
    }
}

[CollectionDefinition("rpm-service-package")]
public class RpmServicePackageCollection : ICollectionFixture<RpmServicePackageFixture>
{
}

public sealed class RpmServicePackageFixture : IAsyncLifetime
{
    private readonly string workingDirectory = IOPath.Combine(IOPath.GetTempPath(), $"rpm-svc-tests-{Guid.NewGuid():N}");
    private readonly string sourceDirectory;

    public RpmServicePackageFixture()
    {
        sourceDirectory = IOPath.Combine(workingDirectory, "publish");
    }

    public byte[] PackageBytes { get; private set; } = Array.Empty<byte>();
    public RpmArchive Archive { get; private set; } = new(new RpmHeader(new Dictionary<int, RpmTagValue>()), Array.Empty<byte>());
    public IReadOnlyList<CpioEntry> PayloadEntries { get; private set; } = Array.Empty<CpioEntry>();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(sourceDirectory);
        await WritePayloadAsync(sourceDirectory);

        var fs = new FileSystem();
        var dirInfo = new DirectoryInfo(sourceDirectory);
        var container = new DirectoryContainer(new DirectoryInfoWrapper(fs, dirInfo)).AsRoot();

        var options = new FromDirectoryOptions();
        options.WithName("My Service");
        options.WithVersion("2.0.0");
        options.WithDescription("A test background service");
        options.WithExecutableName("my-service");
        options.WithService();

        var result = await new RpmPackager().Pack(container, options);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Building service .rpm failed: {result.Error}");
        }

        await using var memStream = new MemoryStream();
        var writeResult = await result.Value.WriteTo(memStream);
        if (writeResult.IsFailure)
        {
            throw new InvalidOperationException($"Writing .rpm failed: {writeResult.Error}");
        }

        PackageBytes = memStream.ToArray();
        Archive = RpmArchiveReader.Read(PackageBytes);

        try
        {
            var payload = DecompressPayload(Archive);
            PayloadEntries = CpioArchiveReader.Read(payload);
        }
        catch
        {
            // CPIO reader has a pre-existing parsing issue; header-only tests still work
        }
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(workingDirectory))
        {
            try { Directory.Delete(workingDirectory, true); }
            catch { }
        }

        return Task.CompletedTask;
    }

    private static async Task WritePayloadAsync(string directory)
    {
        var executablePath = IOPath.Combine(directory, "my-service");
        await File.WriteAllBytesAsync(executablePath, CreateElfStub());
        await File.WriteAllTextAsync(IOPath.Combine(directory, "appsettings.json"), "{ \"Logging\": { \"LogLevel\": { \"Default\": \"Information\" } } }");
    }

    private static byte[] CreateElfStub()
    {
        var bytes = new byte[64];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        bytes[4] = 2;
        bytes[5] = 1;
        BitConverter.GetBytes((ushort)2).CopyTo(bytes, 16);
        BitConverter.GetBytes((ushort)0x3E).CopyTo(bytes, 18);
        return bytes;
    }

    private static byte[] DecompressPayload(RpmArchive archive)
    {
        var compressor = archive.Header.GetString(RpmTestTags.PayloadCompressor);
        if (!string.Equals(compressor, "gzip", StringComparison.OrdinalIgnoreCase))
        {
            return archive.Payload;
        }

        using var input = new MemoryStream(archive.Payload);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
