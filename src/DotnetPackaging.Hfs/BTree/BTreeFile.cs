namespace DotnetPackaging.Hfs.BTree;

/// <summary>
/// Represents a complete B-tree file.
/// Handles node allocation and serialization.
/// </summary>
public sealed class BTreeFile
{
    private readonly List<BTreeNode> nodes = new();
    
    public int NodeSize { get; }
    public BTreeHeaderRecord Header { get; private set; }

    public BTreeFile(int nodeSize = 4096, ushort maxKeyLength = 516)
    {
        NodeSize = nodeSize;
        Header = new BTreeHeaderRecord
        {
            NodeSize = (ushort)nodeSize,
            MaxKeyLength = maxKeyLength,
            TotalNodes = 1,
            FreeNodes = 0,
            TreeDepth = 0,
            RootNode = 0,
            LeafRecords = 0
        };

        // Create header node (node 0)
        var headerNode = BTreeNode.CreateHeaderNode(Header, nodeSize);
        nodes.Add(headerNode);
    }

    /// <summary>
    /// Total number of nodes in the tree.
    /// </summary>
    public int NodeCount => nodes.Count;

    /// <summary>
    /// Gets or sets the root node index.
    /// </summary>
    public uint RootNode
    {
        get => Header.RootNode;
        set => Header = Header with { RootNode = value };
    }

    /// <summary>
    /// Gets or sets the tree depth.
    /// </summary>
    public ushort TreeDepth
    {
        get => Header.TreeDepth;
        set => Header = Header with { TreeDepth = value };
    }

    /// <summary>
    /// Gets the node at the specified index.
    /// </summary>
    public BTreeNode GetNode(int index) => nodes[index];

    /// <summary>
    /// Allocates a new node and returns its index.
    /// </summary>
    public uint AllocateNode(BTreeNode node)
    {
        var index = (uint)nodes.Count;
        nodes.Add(node);
        Header = Header with 
        { 
            TotalNodes = (uint)nodes.Count,
            FreeNodes = 0
        };
        return index;
    }

    /// <summary>
    /// Adds a leaf record to the B-tree.
    /// For simplicity, this implementation stores all records in a single leaf node
    /// or creates additional leaf nodes as needed.
    /// </summary>
    public void AddLeafRecord(byte[] keyAndRecord)
    {
        // If no root exists, create one
        if (Header.TreeDepth == 0)
        {
            var rootNode = BTreeNode.CreateLeafNode(1, NodeSize);
            var rootIndex = AllocateNode(rootNode);
            Header = Header with 
            { 
                RootNode = rootIndex,
                TreeDepth = 1,
                FirstLeafNode = rootIndex,
                LastLeafNode = rootIndex
            };
        }

        // Find the last leaf node
        var lastLeafIndex = Header.LastLeafNode;
        var lastLeaf = nodes[(int)lastLeafIndex];

        // Check if record fits
        if (lastLeaf.CanFit(keyAndRecord.Length))
        {
            lastLeaf.AddRecord(keyAndRecord);
        }
        else
        {
            // Create a new leaf node
            var newLeaf = BTreeNode.CreateLeafNode(1, NodeSize);
            newLeaf.AddRecord(keyAndRecord);
            var newIndex = AllocateNode(newLeaf);

            // Update links
            lastLeaf.Descriptor = lastLeaf.Descriptor with { ForwardLink = newIndex };
            newLeaf.Descriptor = newLeaf.Descriptor with { BackwardLink = lastLeafIndex };

            Header = Header with { LastLeafNode = newIndex };
        }

        Header = Header with { LeafRecords = Header.LeafRecords + 1 };

        // Update header node with new header record
        UpdateHeaderNode();
    }

    /// <summary>
    /// Updates the header node with current header record.
    /// </summary>
    private void UpdateHeaderNode()
    {
        if (nodes.Count > 0)
        {
            var headerNode = nodes[0];
            if (headerNode.Records.Count > 0)
            {
                headerNode.Records[0] = Header.ToBytes();
            }

            // Update map record to reflect allocated nodes
            if (headerNode.Records.Count > 2)
            {
                var mapRecord = headerNode.Records[2];
                var bytesNeeded = (nodes.Count + 7) / 8;
                if (bytesNeeded <= mapRecord.Length)
                {
                    Array.Clear(mapRecord, 0, mapRecord.Length);
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var byteIndex = i / 8;
                        var bitIndex = 7 - (i % 8);
                        mapRecord[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Serializes the entire B-tree to bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[nodes.Count * NodeSize];
        WriteTo(buffer);
        return buffer;
    }

    /// <summary>
    /// Writes the entire B-tree to a span.
    /// </summary>
    public void WriteTo(Span<byte> buffer)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            nodes[i].WriteTo(buffer[(i * NodeSize)..((i + 1) * NodeSize)]);
        }
    }

    /// <summary>
    /// Gets the total size in bytes.
    /// </summary>
    public int TotalSize => nodes.Count * NodeSize;

    /// <summary>
    /// Creates an IByteSource from this B-tree.
    /// </summary>
    public IByteSource ToByteSource()
        => ByteSource.FromBytes(ToBytes());
}
