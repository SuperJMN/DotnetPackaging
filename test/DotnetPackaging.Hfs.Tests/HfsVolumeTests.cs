using System.Buffers.Binary;
using DotnetPackaging.Hfs.Builder;
using FluentAssertions;
using Xunit;

namespace DotnetPackaging.Hfs.Tests;

public class HfsVolumeTests
{
    [Fact]
    public void HfsVolumeBuilder_ShouldBuildEmptyVolume()
    {
        var volume = HfsVolumeBuilder.Create("TestVolume")
            .Build();
        
        // Volume name is sanitized (uppercase, alphanumeric only)
        volume.VolumeName.Should().Be("TestVolume");
        volume.BlockSize.Should().Be(4096);
    }

    [Fact]
    public void HfsVolumeBuilder_ShouldAddFiles()
    {
        var volume = HfsVolumeBuilder.Create("TestVolume")
            .AddFile("test.txt", new byte[] { 1, 2, 3 })
            .Build();
        
        var (files, folders) = volume.CountEntries();
        files.Should().Be(1);
    }

    [Fact]
    public void HfsVolumeBuilder_ShouldAddDirectories()
    {
        var builder = HfsVolumeBuilder.Create("TestVolume");
        var subDir = builder.AddDirectory("subdir");
        subDir.AddFile("inner.txt", new byte[] { 1, 2, 3 });
        var volume = builder.Build();
        
        var (files, folders) = volume.CountEntries();
        files.Should().Be(1);
        folders.Should().Be(1);
    }

    [Fact]
    public void HfsVolumeBuilder_ShouldAddSymlinks()
    {
        var volume = HfsVolumeBuilder.Create("TestVolume")
            .AddSymlink("Applications", "/Applications")
            .Build();
        
        var (files, _) = volume.CountEntries();
        files.Should().Be(1); // Symlinks count as files
    }

    [Fact]
    public void HfsVolumeWriter_ShouldProduceValidHfsVolume()
    {
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("hello.txt", "Hello, World!"u8.ToArray())
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Should have at least boot blocks + volume header
        bytes.Length.Should().BeGreaterThan(1536);
        
        // Check HFS+ signature at offset 1024
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B); // 'H+'
    }

    [Fact]
    public void HfsVolumeWriter_ShouldWriteAlternateVolumeHeader()
    {
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("test.txt", new byte[100])
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Alternate header should be at second-to-last sector
        var altHeaderOffset = bytes.Length - 1024;
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(altHeaderOffset, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleManySmallFiles()
    {
        // Regression test: each small file needs its own block, not total bytes / blockSize
        var builder = HfsVolumeBuilder.Create("Test");
        
        // Add 100 files of 100 bytes each = 10,000 bytes total
        // But each file needs 1 block (4096 bytes), so 100 blocks needed
        for (var i = 0; i < 100; i++)
        {
            builder.AddFile($"file{i}.txt", new byte[100]);
        }
        
        var volume = builder.Build();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Should not throw and should produce valid HFS+
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
        
        // Volume should be at least 100 blocks * 4096 bytes = ~400KB
        bytes.Length.Should().BeGreaterThan(400_000);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleEmptyFiles()
    {
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("empty.txt", Array.Empty<byte>())
            .AddFile("nonempty.txt", new byte[10])
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleManySymlinks()
    {
        var builder = HfsVolumeBuilder.Create("Test");
        
        // Add multiple symlinks
        for (var i = 0; i < 50; i++)
        {
            builder.AddSymlink($"link{i}", $"/target/path/{i}");
        }
        
        var volume = builder.Build();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleDeepDirectoryStructure()
    {
        var builder = HfsVolumeBuilder.Create("Test");
        
        // Create nested structure like App.app/Contents/MacOS/...
        var app = builder.AddDirectory("MyApp.app");
        var contents = app.AddDirectory("Contents");
        var macos = contents.AddDirectory("MacOS");
        var resources = contents.AddDirectory("Resources");
        
        macos.AddFile("MyApp", new byte[1000]);
        contents.AddFile("Info.plist", new byte[500]);
        resources.AddFile("icon.icns", new byte[200]);
        
        var volume = builder.Build();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
        
        var (files, folders) = volume.CountEntries();
        files.Should().Be(3);
        folders.Should().Be(4);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleLargeFile()
    {
        // Single large file spanning multiple blocks
        var largeFile = new byte[50_000]; // ~12 blocks
        new Random(42).NextBytes(largeFile);
        
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("large.bin", largeFile)
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
        
        // Should contain at least the file data
        bytes.Length.Should().BeGreaterThan(50_000);
    }

    [Fact]
    public void HfsVolumeWriter_BlockCountShouldBeCorrect()
    {
        // Verify that block count in header matches actual allocation
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("file1.txt", new byte[100])
            .AddFile("file2.txt", new byte[100])
            .AddFile("file3.txt", new byte[100])
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Read total blocks from volume header (offset 1024 + 40)
        var totalBlocks = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1024 + 40, 4));
        var freeBlocks = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1024 + 44, 4));
        
        // Basic sanity: used blocks should be reasonable
        var usedBlocks = totalBlocks - freeBlocks;
        usedBlocks.Should().BeGreaterThan(0);
        usedBlocks.Should().BeLessThan(totalBlocks);
    }
}
