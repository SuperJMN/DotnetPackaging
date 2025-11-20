using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
using FluentAssertions;
using Xunit;

namespace DotnetPackaging.Exe.Tests;

public class SimpleExePackerTests
{
    [Fact]
    public async Task Should_Create_Valid_Uninstaller_By_Stripping_Payload()
    {
        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("dp-test-strip-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var outputUninstaller = Path.Combine(tempDir.FullName, "Uninstall.exe");
        
        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), "Dummy App");
        
        // Use the real built stub
        var originalStubPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DotnetPackaging.Exe.Installer/bin/Debug/net8.0-windows/DotnetPackaging.Exe.Installer.exe"));
        if (!File.Exists(originalStubPath))
        {
             originalStubPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DotnetPackaging.Exe.Installer/bin/Release/net8.0-windows/DotnetPackaging.Exe.Installer.exe"));
        }
        File.Exists(originalStubPath).Should().BeTrue();

        var metadata = new InstallerMetadata("com.test.strip", "Test Strip", "1.0.0", "Test Vendor", "Desc", "App.exe");

        // Act 1: Build Installer (Stub + Payload)
        await SimpleExePacker.Build(originalStubPath, publishDir.FullName, metadata, outputInstaller);
        
        // Act 2: Strip Payload to create Uninstaller
        // Logic mimics what we will put in Installer.cs
        using (var src = File.OpenRead(outputInstaller))
        {
            var payloadInfo = FindPayloadInfo(src);
            payloadInfo.HasValue.Should().BeTrue();
            var (payloadStart, _) = payloadInfo.Value;

            src.Position = 0;
            using (var dst = File.Create(outputUninstaller))
            {
                var buffer = new byte[81920];
                long remaining = payloadStart;
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = src.Read(buffer, 0, toRead);
                    if (read == 0) break;
                    dst.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
        }

        // Assert
        // The resulting Uninstall.exe should be bit-wise identical to the original Stub
        var originalBytes = await File.ReadAllBytesAsync(originalStubPath);
        var strippedBytes = await File.ReadAllBytesAsync(outputUninstaller);
        
        strippedBytes.Length.Should().Be(originalBytes.Length);
        strippedBytes.Should().Equal(originalBytes);

        // Assert 2: It should run without crashing
        var psi = new ProcessStartInfo(outputUninstaller)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["DP_DUMP_METADATA_JSON"] = "1" } // Just to make it do something and exit
        };
        
        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        var exited = process!.WaitForExit(5000);
        if (!exited) process.Kill();
        
        process.ExitCode.Should().NotBe(-2146233082); // Not 0x80131506

        try { Directory.Delete(tempDir.FullName, true); } catch { }
    }

    [Fact]
    public async Task Should_Extract_Payload_Correctly_From_Installer()
    {
        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("dp-test-extract-");
        var publishDir = tempDir.CreateSubdirectory("publish");
        var outputInstaller = Path.Combine(tempDir.FullName, "Installer.exe");
        var outputExtractDir = tempDir.CreateSubdirectory("extracted");

        var appContent = "Dummy App Content";
        File.WriteAllText(Path.Combine(publishDir.FullName, "App.exe"), appContent);

        var originalStubPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DotnetPackaging.Exe.Installer/bin/Debug/net8.0-windows/DotnetPackaging.Exe.Installer.exe"));
        if (!File.Exists(originalStubPath))
        {
             originalStubPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DotnetPackaging.Exe.Installer/bin/Release/net8.0-windows/DotnetPackaging.Exe.Installer.exe"));
        }
        File.Exists(originalStubPath).Should().BeTrue();

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
}
