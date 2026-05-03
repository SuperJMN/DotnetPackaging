using System.Diagnostics;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Deb;
using Serilog.Core;
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
            ["sample-app"] = FileByteSource.OpenRead("/bin/echo"),
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

    [Fact]
    public async Task FromPublishedProject_WriteTo_Completes_WithCurrentThreadScheduledSources()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deb-lazy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var projectPath = System.IO.Path.Combine(tempDir, "SampleApp.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <AssemblyName>sample-app</AssemblyName>
                <Product>Sample App</Product>
                <Description>Sample package for tests</Description>
              </PropertyGroup>
            </Project>
            """);

        try
        {
            var files = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
            {
                ["sample-app"] = ByteSource.FromBytes(CreateElfStub()),
                ["config/settings.json"] = ByteSource.FromString("{ \"name\": \"demo\" }")
            };

            var container = files.ToRootContainer().Value;
            var context = ProjectPackagingContext.FromProject(projectPath, Logger.None).Value;
            var options = new FromDirectoryOptions();
            options.From(new Options
            {
                Name = Maybe.From("Sample App"),
                Version = Maybe.From("1.0.0"),
                ExecutableName = Maybe.From("sample-app")
            });

            var outputPath = System.IO.Path.Combine(tempDir, "sample.deb");
            var source = new DebPackager().FromPublishedProject(container, context, options, Logger.None);
            var writeTask = Task.Run(() => source.WriteTo(outputPath));

            var completed = await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(5)));
            completed.Should().Be(writeTask, "lazy package byte sources must not block while materializing tar entries");
            var write = await writeTask;
            write.IsSuccess.Should().BeTrue(write.IsFailure ? write.Error : "");
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
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
