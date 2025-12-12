using DotnetPackaging.Dmg.Hfs.Catalog;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotnetPackaging.Dmg.Tests;

public class BTreeSortingTests
{
    private readonly ITestOutputHelper _output;

    public BTreeSortingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CatalogBTree_ShouldMaintainSortedKeys_WhenAddingFiles()
    {
        // Assemble
        // Use small node size to ensure we don't just rely on one node (though for this test 1 node is enough to show sorting fail)
        var catalog = new CatalogBTree(nodeSize: 4096);
        var dirId = new CatalogNodeId(2); // Root
        var fileId1 = new CatalogNodeId(100);
        var fileId2 = new CatalogNodeId(101);
        
        // HFS+ requires keys to be sorted.
        // File 1: Parent=2, Name="FileA" -> Key ~ (2, "FileA")
        // Thread 1: Parent=100, Name=""  -> Key ~ (100, "")
        // File 2: Parent=2, Name="FileB" -> Key ~ (2, "FileB")
        // Thread 2: Parent=101, Name=""  -> Key ~ (101, "")
        
        // Expected Order:
        // (2, "FileA")
        // (2, "FileB")
        // (100, "")
        // (101, "")
        
        // Act
        // Add "FileA"
        catalog.AddFile(dirId, "FileA", new CatalogFileRecord { FileId = fileId1 });
        // Add "FileB"
        catalog.AddFile(dirId, "FileB", new CatalogFileRecord { FileId = fileId2 });

        // Assert
        var btree = catalog.BTree;
        // The implementation creates a header node (0) and a leaf node (1)
        var leafNode = btree.GetNode(1);
        
        // Retrieve keys from records
        var keys = new List<CatalogKey>();
        foreach (var recordBytes in leafNode.Records)
        {
            // Parse Key from record bytes
            // Key length is first 2 bytes (Big Endian)
            // But we can use CatalogKey helper if we could parse it back.
            // Let's manually parse ParentID (bytes 2-6) to check order.
            
            // Key structure: KeyLen(2) + ParentID(4) + NameLen(2) + Name...
            var parentIdVal = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(recordBytes.AsSpan(2, 4));
            var nameLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(recordBytes.AsSpan(6, 2));
            var name = System.Text.Encoding.BigEndianUnicode.GetString(recordBytes.AsSpan(8, nameLen * 2)); // HFS+ regular name encoding check? 
            // Actually implementation uses HfsUnicode which might correspond to this.
            
            _output.WriteLine($"Key: Parent={parentIdVal}, NameLen={nameLen}");
            keys.Add(new CatalogKey { ParentId = new CatalogNodeId(parentIdVal), NodeName = "dummy" }); // Name parsing optional for this check if ParentID is enough
        }

        // Check verification (should be 4 records)
        keys.Count.Should().Be(4);

        // Record 0: FileA (Parent 2)
        keys[0].ParentId.Value.Should().Be(2);
        
        // Record 1: SHOULD BE FileB (Parent 2)
        // With current buggy implementation, it will be ThreadA (Parent 100)
        keys[1].ParentId.Value.Should().Be(2, "Second record should be FileB (Parent 2), not ThreadA (Parent 100)");

        // Check full sort
        // keys should be sorted.
        // (2, 2, 100, 101)
        var parentIds = keys.Select(k => k.ParentId.Value).ToList();
        parentIds.Should().BeInAscendingOrder("Keys must be sorted by ParentID primarily");
    }
}
