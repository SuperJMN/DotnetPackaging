using System.Buffers.Binary;
using DotnetPackaging.Dmg.Hfs.BTree;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotnetPackaging.Dmg.Tests;

public class BTreeTests
{
    private readonly ITestOutputHelper _output;

    public BTreeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HeaderNode_ShouldHaveCorrectLayout()
    {
        // Assemble
        var headerRec = new BTreeHeaderRecord
        {
            NodeSize = 512,
            TotalNodes = 10,
            Attributes = BTreeAttributes.BigKeys
        };
        var node = BTreeNode.CreateHeaderNode(headerRec, 512);

        // Act
        var bytes = node.ToBytes();

        // Assert
        bytes.Length.Should().Be(512);
        
        // Check Node Descriptor (first 14 bytes)
        // Forward link (4 bytes) should be 0 for header/root in simple cases? 
        // Header node is always node 0.
        // Kind = Header (1)
        bytes[8].Should().Be(1); // Kind
        bytes[9].Should().Be(0); // Height
        // NumRecords should be 3 (Header, UserData, Map)
        BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10, 2)).Should().Be(3);

        // Check Record 0 Offset (at end of node, last 2 bytes)
        // Offset for Record 0 should be 14 (NodeDescriptor Size)
        var offset0 = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(510, 2));
        offset0.Should().Be(14, "First record should start after descriptor");

        // Record 1 Offset
        var offset1 = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(508, 2));
        var rec0Len = 106; // Header Record Size
        offset1.Should().Be((ushort)(14 + rec0Len));

        // Record 2 Offset
        var offset2 = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(506, 2));
        var rec1Len = 128; // User Data Record size
        offset2.Should().Be((ushort)(14 + rec0Len + rec1Len));

        // Free Space Offset (last offset)
        var offset3 = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(504, 2));
        // Should point to where free space begins (after Map Record)
        offset3.Should().BeGreaterThan(offset2);
    }
    
    [Fact]
    public void LeafNode_ShouldPlaceOffsetsCorrectly()
    {
        // Assemble
        var node = BTreeNode.CreateLeafNode(nodeSize: 512);
        var record1 = new byte[] { 0xAA, 0xBB, 0xCC };
        var record2 = new byte[] { 0x11, 0x22 };
        
        node.AddRecord(record1);
        node.AddRecord(record2);

        // Act
        var bytes = node.ToBytes();

        // Assert
        // Node Descriptor = 14 bytes.
        // Record 0 starts at 14. Len 3 plus 1 byte of HFS+ even-byte alignment. Ends at 18.
        // Record 1 starts at 18. Len 2. Ends at 20.
        // Free space starts at 20.
        
        // Offsets at end of node:
        // 510-511: Offset 0 (14)
        // 508-509: Offset 1 (18)
        // 506-507: Offset 2 (20) - Start of free space
        
        BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(510, 2)).Should().Be(14);
        BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(508, 2)).Should().Be(18);
        BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(506, 2)).Should().Be(20);

        // Verify content
        bytes[14].Should().Be(0xAA);
        bytes[18].Should().Be(0x11);
    }
}
