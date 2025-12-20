using System.Diagnostics;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using TarEntry = System.Formats.Tar.TarEntry;
using TarReader = System.Formats.Tar.TarReader;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Tests;

[Collection("deb-package")]
public class DebPackageIntegrityTests
{
    private readonly DebPackageFixture fixture;

    public DebPackageIntegrityTests(DebPackageFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Control_and_data_tar_are_readable_with_SystemFormatsTar()
    {
        var controlBytes = fixture.GetEntry("control.tar").Data;
        var controlEntries = ReadTarNames(controlBytes);
        controlEntries.Should().Contain("./control");

        var dataBytes = fixture.GetEntry("data.tar").Data;
        var dataEntries = ReadTarNames(dataBytes);
        dataEntries.Should().Contain("./opt/sample-app/sample-app");
        dataEntries.Should().Contain("./usr/bin/sample-app");
    }

    [Fact]
    public void Ar_members_have_expected_names_and_debian_binary()
    {
        var bytes = fixture.PackageBytes;
        var entries = ArArchiveReader.Read(bytes);

        entries.Select(e => e.Name).Should().ContainInOrder("debian-binary", "control.tar", "data.tar");
        var debianBinary = entries.Single(e => e.Name == "debian-binary");
        Encoding.ASCII.GetString(debianBinary.Data).Should().Be("2.0\n");
    }

    [Fact]
    public async Task Linux_dpkg_deb_accepts_fixture_package()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        await AssertDpkgAccepts(fixture.OutputPath);
    }

    [Fact]
    public async Task Linux_dpkg_deb_accepts_long_paths()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        var files = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
        {
            ["sample-app"] = ByteSource.FromStreamFactory(() => File.OpenRead("/bin/echo")),
            ["very/long/" + new string('a', 80) + "/" + new string('b', 80) + "/deep/" + new string('c', 60) + ".txt"] = ByteSource.FromString("content")
        };

        var rootResult = files.ToRootContainer();
        rootResult.IsSuccess.Should().BeTrue();
        var root = rootResult.Value;

        var options = new Options
        {
            Name = Maybe.From("Sample App"),
            Version = Maybe.From("1.0.0"),
            Comment = Maybe.From("Sample package for tests"),
            ExecutableName = Maybe.From("sample-app")
        };

        var metadata = new FromDirectoryOptions();
        metadata.From(options);

        var deb = await new DotnetPackaging.Deb.DebPackager().Pack(root, metadata);
        deb.IsSuccess.Should().BeTrue();

        var debBytes = deb.Value;
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deb-long-{Guid.NewGuid():N}.deb");
        await using (var stream = File.Create(path))
        {
            var write = await debBytes.WriteTo(stream);
            write.IsSuccess.Should().BeTrue();
        }

        await AssertDpkgAccepts(path);
    }

    private static IReadOnlyList<string> ReadTarNames(byte[] tarBytes)
    {
        using var stream = new MemoryStream(tarBytes);
        using var reader = new TarReader(stream);
        var names = new List<string>();
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            var normalized = entry.Name.Replace("\\", "/", StringComparison.Ordinal);
            if (!normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = "./" + normalized.TrimStart('/');
            }
            names.Add(normalized);
        }

        return names;
    }

    private static async Task AssertDpkgAccepts(string debPath)
    {
        var info = await RunProcess("dpkg-deb", $"--info \"{debPath}\"");
        info.Should().Be(0);

        var contents = await RunProcess("dpkg-deb", $"--contents \"{debPath}\"");
        contents.Should().Be(0);
    }

    private static async Task<int> RunProcess(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return -1;
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
