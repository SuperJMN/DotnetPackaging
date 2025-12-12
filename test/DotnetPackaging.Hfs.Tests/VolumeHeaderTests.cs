using System.Buffers.Binary;
using DotnetPackaging.Dmg.Hfs;
using DotnetPackaging.Dmg.Hfs.Extents;
using DotnetPackaging.Hfs;
using FluentAssertions;
using Xunit;

namespace DotnetPackaging.Hfs.Tests;

public class VolumeHeaderTests
{
    [Fact]
    public void VolumeHeader_ShouldBe512Bytes()
    {
        var header = new VolumeHeader();
        var bytes = header.ToBytes();
        
        bytes.Length.Should().Be(512);
    }

    [Fact]
    public void VolumeHeader_ShouldHaveCorrectSignature()
    {
        var header = new VolumeHeader();
        var bytes = header.ToBytes();
        
        // Signature 'H+' = 0x482B at offset 0
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(0, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void VolumeHeader_ShouldHaveCorrectVersion()
    {
        var header = new VolumeHeader();
        var bytes = header.ToBytes();
        
        // Version 4 at offset 2
        var version = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2, 2));
        version.Should().Be(4);
    }

    [Fact]
    public void VolumeHeader_ShouldHaveCorrectBlockSize()
    {
        var header = new VolumeHeader { BlockSize = 4096 };
        var bytes = header.ToBytes();
        
        // Block size at offset 40
        var blockSize = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(40, 4));
        blockSize.Should().Be(4096);
    }

    [Fact]
    public void VolumeHeader_ShouldPreserveAllFields()
    {
        var now = DateTime.UtcNow;
        var header = new VolumeHeader
        {
            BlockSize = 4096,
            TotalBlocks = 1000,
            FreeBlocks = 500,
            FileCount = 10,
            FolderCount = 5,
            CreateDate = now,
            ModifyDate = now,
            CatalogFile = ForkData.FromExtent(8192, 1, 2)
        };
        
        var bytes = header.ToBytes();
        bytes.Length.Should().Be(512);
        
        // Verify some key fields
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(40, 4)).Should().Be(4096);  // BlockSize
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(44, 4)).Should().Be(1000); // TotalBlocks
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(48, 4)).Should().Be(500);  // FreeBlocks
    }
}
