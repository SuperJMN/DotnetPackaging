using System.Buffers.Binary;
using System.Text;
using DotnetPackaging.Hfs.BTree;
using DotnetPackaging.Hfs.Encoding;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotnetPackaging.Dmg.Tests;

public class InspectDmgTests
{
    private readonly ITestOutputHelper _output;
    private readonly StringBuilder _sb = new();

    public InspectDmgTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    private void Log(string msg)
    {
        _output.WriteLine(msg);
        _sb.AppendLine(msg);
    }

    [Fact]
    public void InspectCatalogRootNode()
    {
        // Must convert first!
        // var dmgPath = "/Users/jmn/RiderProjects/DotnetPackaging/Output_raw.dmg.cdr";
        // Assuming user runs hdiutil convert manually or we use Output.dmg if we fix the reader.
        // For now, let's keep it pointing to cdr but note it might be stale unless we reconvert.
        var dmgPath = "/Users/jmn/RiderProjects/DotnetPackaging/Output_raw.dmg.cdr";
        if (!File.Exists(dmgPath))
        {
            Log("Output.dmg not found.");
            Assert.Fail($"Output.dmg not found at {dmgPath}"); 
            return;
        }

        using var stream = File.OpenRead(dmgPath);
        var buffer = new byte[stream.Length];
        stream.Read(buffer);

        // Debug: Print first 16 bytes and VH start
        var head = BitConverter.ToString(buffer.AsSpan(0, 16).ToArray());
        var vhHead = BitConverter.ToString(buffer.AsSpan(1024, 16).ToArray());
        Log($"File Head (0-16): {head}");
        Log($"VH Head (1024-1040): {vhHead}");

        // 1. Read Volume Header (Sector 2 = 1024 bytes)
        var vhOffset = 1024;
        
        // HFS+ Signature verification
        var signature = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(vhOffset, 2));
        Log($"Signature: 0x{signature:X4} (Expect 0x482B 'H+')");

        // Block Size at offset 40 (0x28)
        var blockSize = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(vhOffset + 40, 4));
        Log($"Block Size: {blockSize}");

        // Catalog File: Offset 272 (0x110)
        // ForkData: LogicalSize(8) @ +0, Clump(4) @ +8, TotalBlocks(4) @ +12, Extents(64) @ +16
        // Extent[0] Start(4) @ +16, Count(4) @ +20
        var catalogSize = BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(vhOffset + 272, 8));
        var catalogStart = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(vhOffset + 272 + 16, 4));
        var catalogCount = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(vhOffset + 272 + 20, 4));

        Log($"Catalog: StartBlock={catalogStart}, Count={catalogCount}, Size={catalogSize}");

        // 2. Read Catalog Data
        var catalogOffset = (int)(catalogStart * blockSize);
        // Note: buffer is byte[], so AsSpan works. But catalogBytes became Span.
        // Let's keep catalogBytes as Span.
        var catalogBytes = buffer.AsSpan(catalogOffset, (int)(catalogCount * blockSize));

        // 3. Parse BTree Header Node (Node 0)
        // Descriptor (14) + HeaderRec (106)
        // catalogBytes is ReadOnlySpan<byte>
        // Use Slice!
        
        var headerRecOffset = 14; 
        var rootNodeId = BinaryPrimitives.ReadUInt32BigEndian(catalogBytes.Slice(headerRecOffset + 2, 4));
        var firstLeaf = BinaryPrimitives.ReadUInt32BigEndian(catalogBytes.Slice(headerRecOffset + 10, 4));
        var lastLeaf = BinaryPrimitives.ReadUInt32BigEndian(catalogBytes.Slice(headerRecOffset + 14, 4));
        var nodeSize = BinaryPrimitives.ReadUInt16BigEndian(catalogBytes.Slice(headerRecOffset + 18, 2));
        var totalNodes = BinaryPrimitives.ReadUInt32BigEndian(catalogBytes.Slice(headerRecOffset + 22, 4));
        
        // Attributes at offset 34 (Header + 34)
        var attributes = BinaryPrimitives.ReadUInt32BigEndian(catalogBytes.Slice(headerRecOffset + 34, 4));

        Log($"Detailed Header Info:");
        Log($"Root Node ID: {rootNodeId}");
        Log($"First Leaf: {firstLeaf}");
        Log($"Last Leaf: {lastLeaf}");
        Log($"Total Nodes: {totalNodes}");
        Log($"Attributes: 0x{attributes:X8}");
        
        // Assert Correct Attributes
        // 0x6 = BigKeys | VariableIndexKeys
        // We accept if at least those bits are set.
        // 5. Inspect Node 10 (Child of Index Rec 8 - Last field in error)
        var childNodeId = 10;
        var childNodeOffset = (int)(childNodeId * nodeSize);
        var childNodeBytes = catalogBytes.Slice(childNodeOffset, nodeSize);
        
        Log($"Inspecting Node {childNodeId} (Last Leaf?):");
        
        var childNumRecords = BinaryPrimitives.ReadUInt16BigEndian(childNodeBytes.Slice(10, 2));
        Log($"Num Recs: {childNumRecords}");
        
        if (childNumRecords > 0)
        {
            // Get FIRST Record (since we care about "First Key" match)
            // Offset is at the end: nodeSize - 2
            var off0 = BinaryPrimitives.ReadUInt16BigEndian(childNodeBytes.Slice(nodeSize - 2, 2));
            var rec0 = childNodeBytes.Slice(off0);
            
            var keyLen = BinaryPrimitives.ReadUInt16BigEndian(rec0.Slice(0, 2));
            var nameLen = BinaryPrimitives.ReadUInt16BigEndian(rec0.Slice(6, 2));
            var nameBytes = rec0.Slice(8, nameLen * 2);
            var nameStr = Encoding.BigEndianUnicode.GetString(nameBytes);
            var parentId = BinaryPrimitives.ReadUInt32BigEndian(rec0.Slice(2, 4));
            
            Log($"Child 10 First Rec Key: [{parentId}, '{nameStr}']");
            
            // Expected Key from Index: [107, '']
            Log($"Expect [107, '']");
        }
        
        var targetNodeId = rootNodeId; 
        var targetNodeOffset = (int)(targetNodeId * nodeSize);
        var targetNodeBytes = catalogBytes.Slice(targetNodeOffset, nodeSize);
        
        Log($"Inspecting Node {targetNodeId} (Root):");

        var fwdLink = BinaryPrimitives.ReadUInt32BigEndian(targetNodeBytes.Slice(0, 4));
        var bwdLink = BinaryPrimitives.ReadUInt32BigEndian(targetNodeBytes.Slice(4, 4));
        var kind = targetNodeBytes[8];
        
        Log($"Fwd Link: {fwdLink}");
        Log($"Bwd Link: {bwdLink}");
        var numRecords = BinaryPrimitives.ReadUInt16BigEndian(targetNodeBytes.Slice(10, 2));
        Log($"Node Kind: {kind} (0=Index, 255=Leaf)");
        Log($"Num Recs: {numRecords}");

        if (kind == 0) // Index Node
        {
            for (int i = 0; i < numRecords; i++)
            {
                var off = BinaryPrimitives.ReadUInt16BigEndian(targetNodeBytes.Slice(nodeSize - 2 - (i * 2), 2));
                var rec = targetNodeBytes.Slice(off);
                
                var keyLen = BinaryPrimitives.ReadUInt16BigEndian(rec.Slice(0, 2));
                // Key: Len(2) + Parent(4) + NameLen(2) + Name(N)
                var nameLen = BinaryPrimitives.ReadUInt16BigEndian(rec.Slice(6, 2));
                var nameBytes = rec.Slice(8, nameLen * 2);
                var nameStr = Encoding.BigEndianUnicode.GetString(nameBytes);
                var parentId = BinaryPrimitives.ReadUInt32BigEndian(rec.Slice(2, 4));
                
                // Pointer is after Key
                var ptrOffset = 2 + keyLen;
                var ptrNodeId = BinaryPrimitives.ReadUInt32BigEndian(rec.Slice(ptrOffset, 4));
                
                Log($"Index Rec {i}: Child={ptrNodeId}, Key=[{parentId}, '{nameStr}']");
                
                // Optional: Inspect Child Last Key
            }
        }
        else if (kind == 255) // Leaf Node
        {
            // Existing leaf inspection logic...
            var off0 = BinaryPrimitives.ReadUInt16BigEndian(targetNodeBytes.Slice(nodeSize - 2, 2));
            if (off0 < nodeSize)
            {
                var rec0 = targetNodeBytes.Slice(off0);
                var keyLen = BinaryPrimitives.ReadUInt16BigEndian(rec0.Slice(0, 2));
                var nameLen = BinaryPrimitives.ReadUInt16BigEndian(rec0.Slice(6, 2));
                var nameBytes = rec0.Slice(8, nameLen * 2);
                var nameStr = Encoding.BigEndianUnicode.GetString(nameBytes);
                Log($"Rec 0 Name: '{nameStr}'");
                
                var dataOffset = 2 + keyLen;
                var recType = BinaryPrimitives.ReadInt16BigEndian(rec0.Slice(dataOffset, 2));
                Log($"Rec 0 Type: 0x{recType:X4} (Expect 0x0001)");
            }
            
            // Last Record?
            var offLast = BinaryPrimitives.ReadUInt16BigEndian(targetNodeBytes.Slice(nodeSize - 2 - ((numRecords - 1) * 2), 2));
            if (offLast < nodeSize)
            {
                var recLast = targetNodeBytes.Slice(offLast);
                var keyLen = BinaryPrimitives.ReadUInt16BigEndian(recLast.Slice(0, 2));
                var dataOffset = 2 + keyLen;
                var recType = BinaryPrimitives.ReadInt16BigEndian(recLast.Slice(dataOffset, 2));
                
                // Assertions
                recType.Should().Be(3, "Record 1 should be Thread Record (0x0003)");
            }
        }
        
        File.WriteAllText("debug_tree.txt", _sb.ToString());
    }
}
