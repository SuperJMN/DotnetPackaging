using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Path = System.IO.Path;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
using DotnetPackaging.Exe.Artifacts;
using DotnetPackaging.Exe.Installer.Core;
using ExeInstallerMetadata = DotnetPackaging.Exe.Metadata.InstallerMetadata;
using FluentAssertions;
using Xunit;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Tests;

public class SimpleExePackerTests
{
    [SkippableFact]
    public async Task Should_Create_Valid_Uninstaller_With_Appended_Payload()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Installer stub is Windows-only.");

        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("dp-test-strip-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var outputUninstaller = Path.Combine(tempDir.FullName, "Uninstaller.exe");
        var metadataDump = Path.Combine(tempDir.FullName, "metadata.json");

        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");

        var metadata = new ExeInstallerMetadata("com.test.strip", "Test Strip", "1.0.0", "Test Vendor", "Desc", "App.exe");

        var artifacts = await BuildArtifacts(publishDir, metadata);
        await WriteArtifacts(artifacts, outputInstaller, outputUninstaller);

        File.Exists(outputUninstaller).Should().BeTrue();
        PayloadExtractor.GetAppendedPayloadStart(outputUninstaller).HasValue.Should().BeTrue();

        var psi = new ProcessStartInfo(outputUninstaller)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = "--uninstall",
            Environment = { ["DP_DUMP_METADATA_JSON"] = metadataDump, ["AVALONIA_HEADLESS"] = "1" }
        };

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        var exited = process!.WaitForExit(5000);
        if (!exited) process.Kill();

        process.ExitCode.Should().Be(0);

        File.Exists(metadataDump).Should().BeTrue();

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [SkippableFact]
    public async Task Should_Extract_Payload_Correctly_From_Installer()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Installer stub is Windows-only.");

        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("dp-test-extract-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");

        var appContent = "Dummy App Content";
        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), appContent);

        var metadata = new ExeInstallerMetadata("com.test.extract", "Test Extract", "1.0.0", "Test Vendor", "Desc", "App.exe");

        var artifacts = await BuildArtifacts(publishDir, metadata);
        await WriteArtifacts(artifacts, outputInstaller, Path.Combine(tempDir.FullName, "Uninstaller.exe"));

        // Act 2: Try to extract payload using the SAME logic as the installer uses
        // We need to invoke PayloadExtractor logic. Since it's internal in another assembly, we'll duplicate the logic or use reflection?
        // Or better: Use the Unzip logic we used in the previous test but actually unzip the content
        
        using (var src = File.OpenRead(outputInstaller))
        {
            var payloadInfo = FindPayloadInfo(src);
            payloadInfo.HasValue.Should().BeTrue();
            var (payloadStart, payloadLen) = payloadInfo.Value;

            src.Position = payloadStart;
            
            // Simulate extraction to a separate stream (as PayloadExtractor does)
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[81920];
                long remaining = payloadLen;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = src.Read(buffer, 0, toRead);
                    if (read == 0) break;
                    ms.Write(buffer, 0, read);
                    remaining -= read;
                }
                ms.Position = 0;

                using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var entry = zipArchive.GetEntry("Content/App.exe");
                    entry.Should().NotBeNull();

                    zipArchive.GetEntry("Support/Uninstaller.exe").Should().NotBeNull();

                    using var entryStream = entry!.Open();
                    using var reader = new StreamReader(entryStream);
                    var content = await reader.ReadToEndAsync();
                    content.Should().Be(appContent);
                }
            }
        }

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [SkippableFact]
    public async Task Installer_build_should_produce_bootable_executable()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Installer stub is Windows-only.");

        var tempDir = Directory.CreateTempSubdirectory("dp-installer-boot-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var metadataDump = Path.Combine(tempDir.FullName, "metadata.json");

        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");

        var metadata = new ExeInstallerMetadata("com.test.boot", "Test Boot", "1.0.0", "Test Vendor", "Desc", "App.exe");
        var artifacts = await BuildArtifacts(publishDir, metadata);
        await WriteArtifacts(artifacts, outputInstaller, Path.Combine(tempDir.FullName, "Uninstaller.exe"));

        var psi = new ProcessStartInfo(outputInstaller)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["DP_DUMP_METADATA_JSON"] = metadataDump,
                ["AVALONIA_HEADLESS"] = "1"
            }
        };

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        process!.WaitForExit(15000).Should().BeTrue();

        process.ExitCode.Should().Be(0);
        File.Exists(metadataDump).Should().BeTrue();

        var json = await File.ReadAllTextAsync(metadataDump);
        var deserialized = JsonSerializer.Deserialize<ExeInstallerMetadata>(json);
        deserialized.Should().NotBeNull();
        deserialized!.ApplicationName.Should().Be(metadata.ApplicationName);

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [SkippableFact]
    public async Task Uninstaller_should_be_stripped_and_bootable()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Installer stub is Windows-only.");

        var tempDir = Directory.CreateTempSubdirectory("dp-uninstall-boot-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var outputUninstaller = Path.Combine(tempDir.FullName, "Uninstaller.exe");
        var metadataDump = Path.Combine(tempDir.FullName, "metadata.json");

        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");

        var metadata = new ExeInstallerMetadata("com.test.uninstall", "Test Uninstall", "1.0.0", "Test Vendor", "Desc", "App.exe");
        var artifacts = await BuildArtifacts(publishDir, metadata);
        await WriteArtifacts(artifacts, outputInstaller, outputUninstaller);

        PayloadExtractor.GetAppendedPayloadStart(outputInstaller).HasValue.Should().BeTrue();

        File.Exists(outputUninstaller).Should().BeTrue();
        PayloadExtractor.GetAppendedPayloadStart(outputUninstaller).HasValue.Should().BeTrue();

        var psi = new ProcessStartInfo(outputUninstaller)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = "--uninstall",
            Environment =
            {
                ["DP_DUMP_METADATA_JSON"] = metadataDump,
                ["AVALONIA_HEADLESS"] = "1"
            }
        };

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        process!.WaitForExit(15000).Should().BeTrue();

        process.ExitCode.Should().Be(0);
        File.Exists(metadataDump).Should().BeTrue();

        var json = await File.ReadAllTextAsync(metadataDump);
        var deserialized = JsonSerializer.Deserialize<ExeInstallerMetadata>(json);
        deserialized.Should().NotBeNull();
        deserialized!.ApplicationName.Should().Be(metadata.ApplicationName);

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [SkippableFact]
    public async Task Dispatcher_failures_should_be_logged()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Installer stub is Windows-only.");

        var tempDir = Directory.CreateTempSubdirectory("dp-dispatcher-log-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var logPath = GetLogPath();

        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }

        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");

        var metadata = new ExeInstallerMetadata("com.test.dispatcher", "Test Dispatcher", "1.0.0", "Test Vendor", "Desc", "App.exe");
        var artifacts = await BuildArtifacts(publishDir, metadata);
        await WriteArtifacts(artifacts, outputInstaller, Path.Combine(tempDir.FullName, "Uninstaller.exe"));

        var psi = new ProcessStartInfo(outputInstaller)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["DP_FORCE_DISPATCHER_FAILURE"] = "1",
                ["AVALONIA_HEADLESS"] = "1"
            }
        };

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        process!.WaitForExit(15000).Should().BeTrue();

        File.Exists(logPath).Should().BeTrue();
        var logContent = await File.ReadAllTextAsync(logPath);
        logContent.Should().Contain("Failed to start installer dispatcher");

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [Fact]
    public async Task Metadata_payload_should_expose_logo_from_disk()
    {
        var tempDir = Directory.CreateTempSubdirectory("dp-meta-logo-");
        var metadata = new ExeInstallerMetadata("com.test.logo", "Test Logo", "1.0.0", "Test Vendor", HasLogo: true);
        var metadataPath = Path.Combine(tempDir.FullName, "metadata.json");
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata));

        var logoBytes = new byte[] { 1, 2, 3, 4, 5 };
        var logoPath = Path.Combine(tempDir.FullName, "logo.png");
        await File.WriteAllBytesAsync(logoPath, logoBytes);

        var payloadResult = MetadataFilePayload.FromDirectory(tempDir.FullName);
        payloadResult.IsSuccess.Should().BeTrue(payloadResult.IsFailure ? payloadResult.Error : string.Empty);

        var logoResult = await payloadResult.Value.GetLogo();
        logoResult.IsSuccess.Should().BeTrue(logoResult.IsFailure ? logoResult.Error : string.Empty);
        logoResult.Value.HasValue.Should().BeTrue();

        await using var logoStream = logoResult.Value.Value.ToStreamSeekable();
        await using var buffer = new MemoryStream();
        await logoStream.CopyToAsync(buffer);
        buffer.ToArray().Should().Equal(logoBytes);

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [Fact]
    public async Task Payload_should_be_readable_without_disk_intermediates()
    {
        var metadata = new ExeInstallerMetadata("com.test.memory", "Memory Test", "1.0.0", "Vendor", ExecutableName: "App.exe");
        var containerResult = new Dictionary<string, IByteSource>
        {
            ["App.exe"] = ByteSource.FromString("App content"),
            ["data/config.json"] = ByteSource.FromString("{\"enabled\":true}")
        }.ToRootContainer();

        containerResult.IsSuccess.Should().BeTrue(containerResult.IsFailure ? containerResult.Error : string.Empty);

        var stub = ByteSource.FromBytes(new byte[] { 1, 2, 3, 4 });
        var buildResult = await SimpleExePacker.Build(stub, containerResult.Value, metadata, Maybe<IByteSource>.None);
        buildResult.IsSuccess.Should().BeTrue(buildResult.IsFailure ? buildResult.Error : string.Empty);

        await using var stream = buildResult.Value.Installer.ToStreamSeekable();
        var payloadBytesMaybe = ExtractPayloadBytes(stream);
        payloadBytesMaybe.HasValue.Should().BeTrue();
        var payloadBytes = payloadBytesMaybe.Value;

        using var archive = new ZipArchive(new MemoryStream(payloadBytes), ZipArchiveMode.Read, leaveOpen: false);
        archive.GetEntry("Support/Uninstaller.exe").Should().NotBeNull();

        var appEntry = archive.GetEntry("Content/App.exe");
        appEntry.Should().NotBeNull();
        using (var entryStream = appEntry!.Open())
        using (var reader = new StreamReader(entryStream))
        {
            (await reader.ReadToEndAsync()).Should().Be("App content");
        }

        var metadataEntry = archive.GetEntry("metadata.json");
        metadataEntry.Should().NotBeNull();
        using (var entryStream = metadataEntry!.Open())
        using (var reader = new StreamReader(entryStream))
        {
            var serialized = await reader.ReadToEndAsync();
            serialized.Should().Contain("Memory Test");
        }
    }

    private async Task<ExeBuildArtifacts> BuildArtifacts(DirectoryInfo publishDir, ExeInstallerMetadata metadata, Maybe<IByteSource>? logo = null)
    {
        var container = BuildContainer(publishDir);
        var stub = ByteSource.FromStreamFactory(() => File.OpenRead(ResolveStubPath()));
        var buildResult = await SimpleExePacker.Build(stub, container, metadata, logo ?? Maybe<IByteSource>.None);
        buildResult.IsSuccess.Should().BeTrue(buildResult.IsFailure ? buildResult.Error : string.Empty);
        return buildResult.Value;
    }

    private static RootContainer BuildContainer(DirectoryInfo publishDir)
    {
        var files = Directory
            .EnumerateFiles(publishDir.FullName, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(publishDir.FullName, file).Replace('\\', '/'),
                file => (IByteSource)ByteSource.FromStreamFactory(() => File.OpenRead(file)),
                StringComparer.Ordinal);

        var containerResult = files.ToRootContainer();
        containerResult.IsSuccess.Should().BeTrue(containerResult.IsFailure ? containerResult.Error : string.Empty);
        return containerResult.Value;
    }

    private static async Task WriteArtifacts(ExeBuildArtifacts artifacts, string installerPath, string uninstallerPath)
    {
        var writeResult = await artifacts.WriteTo(new FileInfo(installerPath), new FileInfo(uninstallerPath));
        writeResult.IsSuccess.Should().BeTrue(writeResult.IsFailure ? writeResult.Error : string.Empty);
    }

    private Maybe<(long Start, long Length)> FindPayloadInfo(Stream stream)
    {
        var searchWindow = 4096;
        if (stream.Length < searchWindow) searchWindow = (int)stream.Length;

        var buffer = new byte[searchWindow];
        stream.Seek(-searchWindow, SeekOrigin.End);
        var bytesRead = stream.Read(buffer, 0, searchWindow);
        
        var magic = "DPACKEXE1";
        var magicBytes = Encoding.ASCII.GetBytes(magic);
        
        var magicPos = -1;
        for (int i = buffer.Length - magicBytes.Length; i >= 0; i--)
        {
            bool match = true;
            for (int j = 0; j < magicBytes.Length; j++)
            {
                if (buffer[i + j] != magicBytes[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) 
            {
                magicPos = i;
                break;
            }
        }

        if (magicPos == -1) return Maybe<(long, long)>.None;

        var footerStartInFile = stream.Length - bytesRead + magicPos;
        var lengthPos = footerStartInFile - 8;

        stream.Seek(lengthPos, SeekOrigin.Begin);
        var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var payloadLen = reader.ReadInt64();

        return (lengthPos - payloadLen, payloadLen);
    }

    private Maybe<byte[]> ExtractPayloadBytes(Stream stream)
    {
        var info = FindPayloadInfo(stream);
        if (info.HasNoValue)
        {
            return Maybe<byte[]>.None;
        }

        var (start, length) = info.Value;
        stream.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[length];
        var read = 0;

        while (read < length)
        {
            var chunk = stream.Read(buffer, read, (int)(length - read));
            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        return Maybe<byte[]>.From(buffer);
    }

    private static string ResolveStubPath()
    {
        var debugPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DotnetPackaging.Exe.Installer/bin/Debug/net8.0-windows/DotnetPackaging.Exe.Installer.exe"));
        var releasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DotnetPackaging.Exe.Installer/bin/Release/net8.0-windows/DotnetPackaging.Exe.Installer.exe"));

        if (File.Exists(debugPath))
        {
            return debugPath;
        }

        if (File.Exists(releasePath))
        {
            return releasePath;
        }

        throw new SkipException("Installer stub not found. Build DotnetPackaging.Exe.Installer first.");
    }

    private static string GetLogPath(bool isUninstaller = false)
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "DotnetPackaging.Installer");
        return Path.Combine(logDirectory, isUninstaller ? "uninstaller.log" : "installer.log");
    }
}
