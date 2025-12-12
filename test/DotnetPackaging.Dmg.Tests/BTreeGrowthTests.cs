using DotnetPackaging.Dmg.Hfs.BTree;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotnetPackaging.Dmg.Tests;

public class BTreeGrowthTests
{
    private readonly ITestOutputHelper _output;

    public BTreeGrowthTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BTree_ShouldIncreaseDepth_WhenLeafFillsUp()
    {
        // Assemble
        // Use small node size to force split quickly
        // Header (512)
        var btree = new BTreeFile(nodeSize: 512);
        
        // Record size = 100 bytes.
        // Node capacity ~ 512 - 14 (descriptor) - 2 (offsets) = 496.
        // Can fit 4 records (400 bytes + 8 overhead).
        // 5th record should force split.
        
        var record = new byte[100]; 
        
        // Act
        // Use BuildFromSortedRecords logic
        // Need to provide Key + Record.
        // For testing, key can be simple.
        var items = new List<(byte[] Key, byte[] Record)>();
        for (int i = 0; i < 10; i++)
        {
            // Fake key: KeyLen(2) + 4 bytes.
            var key = new byte[6];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(key.AsSpan(0, 2), 4);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(2, 4), i);
            items.Add((key, record));
        }

        btree.BuildFromSortedRecords(items);

        // Assert
        // With 10 records, we need ~3 leaf nodes.
        // Depth should be at least 2 (Index -> Leaves), or 3 depending on branching factor.
        // If Depth is 1, it's a bug (Linear list of leaves is invalid for Root).
        
        _output.WriteLine($"Tree Depth: {btree.TreeDepth}");
        _output.WriteLine($"Total Nodes: {btree.NodeCount}");
        
        btree.TreeDepth.Should().BeGreaterThan(1, "Tree must grow in height when nodes split");
    }
}
