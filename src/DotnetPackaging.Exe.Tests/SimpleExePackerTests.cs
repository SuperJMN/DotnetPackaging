using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
using DotnetPackaging.Exe.Installer.Core;
using FluentAssertions;
using Xunit;

namespace DotnetPackaging.Exe.Tests;

public class SimpleExePackerTests
{
    [SkippableFact]
    public async Task Should_Create_Valid_Uninstaller_By_Stripping_Payload()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Installer stub is Windows-only.");

        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("dp-test-strip-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var outputUninstaller = Path.Combine(tempDir.FullName, "Uninstall.exe");

        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");

        var stubPath = ResolveStubPath();
        var metadata = new InstallerMetadata("com.test.strip", "Test Strip", "1.0.0", "Test Vendor", "Desc", "App.exe");

        // Act 1: Build Installer (Stub + Payload)
        await SimpleExePacker.Build(stubPath, publishDir.FullName, metadata, outputInstaller);

        // Act 2: Strip Payload to create Uninstaller
        using (var src = File.OpenRead(outputInstaller))
        {
            var payloadInfo = FindPayloadInfo(src);
            payloadInfo.HasValue.Should().BeTrue();
            var (payloadStart, _) = payloadInfo.Value;

            src.Position = 0;
            using var dst = File.Create(outputUninstaller);
            var buffer = new byte[81920];
            long remaining = payloadStart;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = src.Read(buffer, 0, toRead);
                if (read == 0) break;
                dst.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        // Assert
        var originalBytes = await File.ReadAllBytesAsync(stubPath);
        var strippedBytes = await File.ReadAllBytesAsync(outputUninstaller);

        strippedBytes.Length.Should().Be(originalBytes.Length);
        strippedBytes.Should().Equal(originalBytes);

        var psi = new ProcessStartInfo(outputUninstaller)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["DP_DUMP_METADATA_JSON"] = "1", ["AVALONIA_HEADLESS"] = "1" }
        };

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        var exited = process!.WaitForExit(5000);
        if (!exited) process.Kill();

        process.ExitCode.Should().Be(0);

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

        var originalStubPath = ResolveStubPath();

        var metadata = new InstallerMetadata("com.test.extract", "Test Extract", "1.0.0", "Test Vendor", "Desc", "App.exe");

        // Act 1: Build Installer
        await SimpleExePacker.Build(originalStubPath, publishDir.FullName, metadata, outputInstaller);

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

        var metadata = new InstallerMetadata("com.test.boot", "Test Boot", "1.0.0", "Test Vendor", "Desc", "App.exe");
        await SimpleExePacker.Build(ResolveStubPath(), publishDir.FullName, metadata, outputInstaller);

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
        var deserialized = JsonSerializer.Deserialize<InstallerMetadata>(json);
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
        var uninstallDir = tempDir.CreateSubdirectory("Uninstall");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var outputUninstaller = Path.Combine(uninstallDir.FullName, "Uninstall.exe");
        var metadataDump = Path.Combine(uninstallDir.FullName, "metadata.json");

        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");

        var metadata = new InstallerMetadata("com.test.uninstall", "Test Uninstall", "1.0.0", "Test Vendor", "Desc", "App.exe");
        await SimpleExePacker.Build(ResolveStubPath(), publishDir.FullName, metadata, outputInstaller);

        PayloadExtractor.GetAppendedPayloadStart(outputInstaller).HasValue.Should().BeTrue();

        var slimCopy = UninstallerBuilder.CreateSlimCopy(outputInstaller, outputUninstaller);
        slimCopy.IsSuccess.Should().BeTrue(slimCopy.IsFailure ? slimCopy.Error : string.Empty);

        PayloadExtractor.GetAppendedPayloadStart(outputUninstaller).HasValue.Should().BeFalse();

        var metadataJson = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(Path.Combine(uninstallDir.FullName, "metadata.json"), metadataJson);

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
        var deserialized = JsonSerializer.Deserialize<InstallerMetadata>(json);
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

        var metadata = new InstallerMetadata("com.test.dispatcher", "Test Dispatcher", "1.0.0", "Test Vendor", "Desc", "App.exe");
        await SimpleExePacker.Build(ResolveStubPath(), publishDir.FullName, metadata, outputInstaller);

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

    private Maybe<(long Start, long Length)> FindPayloadInfo(FileStream fs)
    {
        var searchWindow = 4096;
        if (fs.Length < searchWindow) searchWindow = (int)fs.Length;
        
        var buffer = new byte[searchWindow];
        fs.Seek(-searchWindow, SeekOrigin.End);
        fs.Read(buffer, 0, searchWindow);
        
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

        var footerStartInFile = fs.Length - searchWindow + magicPos;
        var lengthPos = footerStartInFile - 8;
        
        fs.Seek(lengthPos, SeekOrigin.Begin);
        var reader = new BinaryReader(fs, Encoding.UTF8, true);
        var payloadLen = reader.ReadInt64();

        return (lengthPos - payloadLen, payloadLen);
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
