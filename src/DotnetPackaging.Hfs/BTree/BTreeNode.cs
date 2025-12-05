using DotnetPackaging.Hfs.Encoding;

namespace DotnetPackaging.Hfs.BTree;

/// <summary>
/// Generic B-tree node that can hold records of any type.
/// Each node has a fixed size and contains:
/// - Node descriptor (14 bytes)
/// - Records (variable)
/// - Free space
/// - Record offsets (from end of node)
/// </summary>
public sealed class BTreeNode
{
    public NodeDescriptor Descriptor { get; set; } = new();
    public List<byte[]> Records { get; } = new();
    public int NodeSize { get; }

    public BTreeNode(int nodeSize = 4096)
    {
        NodeSize = nodeSize;
    }

    /// <summary>
    /// Calculates the used space in this node.
    /// </summary>
    public int UsedSpace
    {
        get
        {
            // Descriptor + records + offset table (2 bytes per record + 2 for free space offset)
            var recordsSize = Records.Sum(r => r.Length);
            var offsetsSize = (Records.Count + 1) * 2;
            return NodeDescriptor.Size + recordsSize + offsetsSize;
        }
    }

    /// <summary>
    /// Calculates the free space available in this node.
    /// </summary>
    public int FreeSpace => NodeSize - UsedSpace;

    /// <summary>
    /// Checks if a record of the given size can fit in this node.
    /// </summary>
    public bool CanFit(int recordSize)
        => FreeSpace >= recordSize + 2; // +2 for offset entry

    /// <summary>
    /// Adds a record to this node.
    /// </summary>
    public void AddRecord(byte[] record)
    {
        Records.Add(record);
        Descriptor = Descriptor with { NumRecords = (ushort)Records.Count };
    }

    /// <summary>
    /// Serializes this node to a byte array of exactly NodeSize bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[NodeSize];
        WriteTo(buffer);
        return buffer;
    }

    /// <summary>
    /// Writes this node to a span.
    /// </summary>
    public void WriteTo(Span<byte> buffer)
    {
        // Write node descriptor
        Descriptor.WriteTo(buffer[..NodeDescriptor.Size]);

        // Write records starting after descriptor
        var recordOffset = NodeDescriptor.Size;
        var offsets = new List<ushort> { (ushort)recordOffset };

        foreach (var record in Records)
        {
            record.CopyTo(buffer[recordOffset..]);
            recordOffset += record.Length;
            offsets.Add((ushort)recordOffset);
        }

        // Write offset table at end of node (in reverse order)
        // Offsets are stored as 16-bit big-endian values from the end of the node
        var offsetPos = NodeSize - 2;
        foreach (var offset in offsets)
        {
            BigEndianWriter.WriteUInt16(buffer[offsetPos..], offset);
            offsetPos -= 2;
        }
    }

    /// <summary>
    /// Creates a header node for a B-tree.
    /// </summary>
    public static BTreeNode CreateHeaderNode(BTreeHeaderRecord header, int nodeSize = 4096)
    {
        var node = new BTreeNode(nodeSize)
        {
            Descriptor = new NodeDescriptor
            {
                Kind = NodeKind.Header,
                Height = 0,
                NumRecords = 3 // Header record, user data record, map record
            }
        };

        // Record 0: Header record
        node.Records.Add(header.ToBytes());

        // Record 1: User data record (128 bytes, reserved)
        node.Records.Add(new byte[128]);

        // Record 2: Map record (bitmap for nodes in this map node)
        // Size = nodeSize - 256 (header + records + offsets)
        // But we simplify: just the remaining space
        var mapSize = nodeSize - NodeDescriptor.Size - BTreeHeaderRecord.Size - 128 - 8;
        if (mapSize < 0) mapSize = 256;
        var mapRecord = new byte[mapSize];
        // First two bits are set (header node and root node are allocated)
        mapRecord[0] = 0xC0; // Binary: 11000000
        node.Records.Add(mapRecord);

        return node;
    }

    /// <summary>
    /// Creates an empty leaf node.
    /// </summary>
    public static BTreeNode CreateLeafNode(byte height = 1, int nodeSize = 4096)
    {
        return new BTreeNode(nodeSize)
        {
            Descriptor = new NodeDescriptor
            {
                Kind = NodeKind.Leaf,
                Height = height
            }
        };
    }

    /// <summary>
    /// Creates an index node.
    /// </summary>
    public static BTreeNode CreateIndexNode(byte height, int nodeSize = 4096)
    {
        return new BTreeNode(nodeSize)
        {
            Descriptor = new NodeDescriptor
            {
                Kind = NodeKind.Index,
                Height = height
            }
        };
    }
}
