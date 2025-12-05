using DotnetPackaging.Hfs.BTree;
using DotnetPackaging.Hfs.Catalog;
using FluentAssertions;
using Xunit;

namespace DotnetPackaging.Hfs.Tests;

public class BTreeTests
{
    [Fact]
    public void BTreeFile_ShouldCreateHeaderNode()
    {
        var btree = new BTreeFile(4096);
        
        btree.NodeCount.Should().Be(1);
        btree.Header.TotalNodes.Should().Be(1);
    }

    [Fact]
    public void BTreeFile_ShouldAddLeafRecords()
    {
        var btree = new BTreeFile(4096);
        
        btree.AddLeafRecord(new byte[100]);
        btree.AddLeafRecord(new byte[100]);
        
        btree.Header.LeafRecords.Should().Be(2);
        btree.Header.TreeDepth.Should().Be(1);
    }

    [Fact]
    public void BTreeFile_ShouldSerializeToCorrectSize()
    {
        var btree = new BTreeFile(4096);
        btree.AddLeafRecord(new byte[100]);
        
        var bytes = btree.ToBytes();
        
        // Should have header node + root leaf node
        bytes.Length.Should().Be(btree.NodeCount * 4096);
    }

    [Fact]
    public void NodeDescriptor_ShouldBe14Bytes()
    {
        var descriptor = new NodeDescriptor
        {
            Kind = NodeKind.Leaf,
            Height = 1,
            NumRecords = 5
        };
        
        var bytes = descriptor.ToBytes();
        bytes.Length.Should().Be(14);
    }

    [Fact]
    public void BTreeHeaderRecord_ShouldBe106Bytes()
    {
        var header = new BTreeHeaderRecord
        {
            NodeSize = 4096,
            TreeDepth = 1
        };
        
        var bytes = header.ToBytes();
        bytes.Length.Should().Be(106);
    }
}
