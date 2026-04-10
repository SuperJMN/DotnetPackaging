using CSharpFunctionalExtensions;
using DotnetPackaging;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using IOPath = System.IO.Path;

namespace DotnetPackaging.Deb.Tests;

[Collection("deb-service-package")]
public class DebServicePackageTests
{
    private readonly DebServicePackageFixture fixture;

    public DebServicePackageTests(DebServicePackageFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Ar_archive_contains_required_members()
    {
        fixture.ArEntries.Should().HaveCount(3);
        fixture.ArEntries.Select(e => e.Name).Should().ContainInOrder("debian-binary", "control.tar", "data.tar");
    }

    [Fact]
    public void Control_tar_contains_maintainer_scripts()
    {
        var controlEntry = fixture.GetEntry("control.tar");
        var controlTar = TarArchiveReader.ReadEntries(controlEntry.Data);
        var names = controlTar.Select(e => e.Name).ToList();

        names.Should().Contain("control");
        names.Should().Contain("postinst");
        names.Should().Contain("prerm");
        names.Should().Contain("postrm");
    }

    [Fact]
    public void Postinst_enables_and_starts_service()
    {
        var controlEntry = fixture.GetEntry("control.tar");
        var controlTar = TarArchiveReader.ReadEntries(controlEntry.Data);
        var postinst = controlTar.Single(e => e.Name == "postinst");
        var text = Encoding.ASCII.GetString(postinst.Data);

        text.Should().Contain("systemctl daemon-reload");
        text.Should().Contain("systemctl enable my-service.service");
        text.Should().Contain("systemctl start my-service.service");
        text.Should().Contain("configure");
    }

    [Fact]
    public void Prerm_stops_and_disables_service()
    {
        var controlEntry = fixture.GetEntry("control.tar");
        var controlTar = TarArchiveReader.ReadEntries(controlEntry.Data);
        var prerm = controlTar.Single(e => e.Name == "prerm");
        var text = Encoding.ASCII.GetString(prerm.Data);

        text.Should().Contain("systemctl stop my-service.service");
        text.Should().Contain("systemctl disable my-service.service");
    }

    [Fact]
    public void Postrm_reloads_daemon_on_purge()
    {
        var controlEntry = fixture.GetEntry("control.tar");
        var controlTar = TarArchiveReader.ReadEntries(controlEntry.Data);
        var postrm = controlTar.Single(e => e.Name == "postrm");
        var text = Encoding.ASCII.GetString(postrm.Data);

        text.Should().Contain("systemctl daemon-reload");
        text.Should().Contain("purge");
    }

    [Fact]
    public void Data_tar_contains_systemd_unit_file()
    {
        var dataEntry = fixture.GetEntry("data.tar");
        var dataTar = TarArchiveReader.ReadEntries(dataEntry.Data);

        dataTar.Select(e => e.Name).Should().Contain("lib/systemd/system/my-service.service");
    }

    [Fact]
    public void Systemd_unit_file_has_correct_content()
    {
        var dataEntry = fixture.GetEntry("data.tar");
        var dataTar = TarArchiveReader.ReadEntries(dataEntry.Data);
        var unitFile = dataTar.Single(e => e.Name == "lib/systemd/system/my-service.service");
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
    public void Data_tar_does_not_contain_desktop_file()
    {
        var dataEntry = fixture.GetEntry("data.tar");
        var dataTar = TarArchiveReader.ReadEntries(dataEntry.Data);

        dataTar.Select(e => e.Name).Should().NotContain(n => n.Contains(".desktop"));
    }

    [Fact]
    public void Data_tar_still_contains_usr_bin_wrapper()
    {
        var dataEntry = fixture.GetEntry("data.tar");
        var dataTar = TarArchiveReader.ReadEntries(dataEntry.Data);

        dataTar.Select(e => e.Name).Should().Contain("usr/bin/my-service");

        var wrapper = dataTar.Single(e => e.Name == "usr/bin/my-service");
        var text = Encoding.ASCII.GetString(wrapper.Data);
        text.Should().StartWith("#!/usr/bin/env sh");
        text.Should().Contain("/opt/my-service/my-service");
    }

    [Fact]
    public void Data_tar_contains_application_files()
    {
        var dataEntry = fixture.GetEntry("data.tar");
        var dataTar = TarArchiveReader.ReadEntries(dataEntry.Data);

        dataTar.Select(e => e.Name).Should().Contain("opt/my-service/my-service");
        dataTar.Select(e => e.Name).Should().Contain("opt/my-service/appsettings.json");
    }

    [Fact]
    public void Maintainer_scripts_are_executable()
    {
        var controlEntry = fixture.GetEntry("control.tar");
        var controlTar = TarArchiveReader.ReadEntries(controlEntry.Data);

        foreach (var scriptName in new[] { "postinst", "prerm", "postrm" })
        {
            var script = controlTar.Single(e => e.Name == scriptName);
            // Mode should be 755 (rwxr-xr-x)
            script.Mode.Should().HaveFlag(UnixFileMode.UserExecute, $"{scriptName} should be executable");
        }
    }
}

[CollectionDefinition("deb-service-package")]
public class DebServicePackageCollection : ICollectionFixture<DebServicePackageFixture>
{
}

public sealed class DebServicePackageFixture : IAsyncLifetime
{
    private readonly string workingDirectory = IOPath.Combine(IOPath.GetTempPath(), $"deb-svc-tests-{Guid.NewGuid():N}");
    private readonly string sourceDirectory;
    private readonly string outputFilePath;

    public DebServicePackageFixture()
    {
        sourceDirectory = IOPath.Combine(workingDirectory, "publish");
        outputFilePath = IOPath.Combine(workingDirectory, "my-service.deb");
    }

    public IReadOnlyList<ArEntry> ArEntries { get; private set; } = Array.Empty<ArEntry>();
    public byte[] PackageBytes { get; private set; } = Array.Empty<byte>();
    public ArEntry GetEntry(string name) => ArEntries.Single(e => string.Equals(e.Name, name, StringComparison.Ordinal));

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

        var result = await new DebPackager().Pack(container, options);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Building service .deb failed: {result.Error}");
        }

        using var memStream = new MemoryStream();
        var writeResult = await result.Value.WriteTo(memStream);
        if (writeResult.IsFailure)
        {
            throw new InvalidOperationException($"Writing .deb failed: {writeResult.Error}");
        }

        PackageBytes = memStream.ToArray();
        ArEntries = ArArchiveReader.Read(PackageBytes);
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
}
