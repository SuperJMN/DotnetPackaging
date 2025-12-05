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
}
