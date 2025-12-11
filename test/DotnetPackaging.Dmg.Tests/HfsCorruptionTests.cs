using System.Diagnostics;
using DotnetPackaging.Dmg.Hfs.Builder;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotnetPackaging.Dmg.Tests;

public class HfsCorruptionTests
{
    private readonly ITestOutputHelper _output;

    public HfsCorruptionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ShouldCreateValidHfsVolume()
    {
        // Assemble
        var builder = HfsVolumeBuilder.Create("TestHFS")
            .WithBlockSize(4096);

        // Add enough files/content to trigger potential allocation logic issues
        // "Invalid extent entry" might be related to how extents are recorded for files
        // or the catalog file itself.
        for (int i = 0; i < 10; i++)
        {
            builder.AddFile($"file_{i}.txt", System.Text.Encoding.UTF8.GetBytes($"Hello world {i}"));
        }
        
        // Add a slightly larger file
        var largeData = new byte[1024 * 100]; // 100KB
        new Random(42).NextBytes(largeData);
        builder.AddFile("large_file.bin", largeData);

        var volume = builder.Build();

        // Act
        // var byteSource = volume.ToByteSource();
        // var bytes = await byteSource.ReadAllBytes();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var tempFile = Path.GetTempFileName();
        // Rename to .img to match convention? Not strictly necessary for fsck_hfs but helper.
        var imgPath = Path.ChangeExtension(tempFile, ".hfs");
        File.Move(tempFile, imgPath);
        
        try
        {
            await File.WriteAllBytesAsync(imgPath, bytes);
            _output.WriteLine($"Generated HFS+ image at: {imgPath}");

            // Verify
            var (exitCode, stdout, stderr) = await RunFsck(imgPath);
            
            _output.WriteLine("fsck_hfs output:");
            _output.WriteLine(stdout);
            if (!string.IsNullOrEmpty(stderr))
            {
                _output.WriteLine("fsck_hfs errors:");
                _output.WriteLine(stderr);
            }

            // Assert
            exitCode.Should().Be(0, "fsck_hfs should return 0 for a valid volume");
        }
        finally
        {
            if (File.Exists(imgPath))
            {
                File.Delete(imgPath);
            }
        }
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunFsck(string imagePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "fsck_hfs",
            Arguments = $"-n \"{imagePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
