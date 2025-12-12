using DotnetPackaging.Dmg.Hfs.Encoding;

namespace DotnetPackaging.Dmg.Hfs.BTree;

/// <summary>
/// Represents a complete B-tree file.
/// Handles node allocation and serialization.
/// </summary>
public sealed class BTreeFile
{
    private readonly List<BTreeNode> nodes = new();
    
    public int NodeSize { get; }
    public BTreeHeaderRecord Header { get; private set; }

    public BTreeFile(int nodeSize = 4096, ushort maxKeyLength = 516, BTreeAttributes attributes = BTreeAttributes.BigKeys)
    {
        NodeSize = nodeSize;
        Header = new BTreeHeaderRecord
        {
            NodeSize = (ushort)nodeSize,
            MaxKeyLength = maxKeyLength,
            Attributes = attributes,
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
    /// <summary>
    /// Builds the B-tree from a sorted list of records.
    /// This replaces the incremental AddLeafRecord approach.
    /// </summary>
    public void BuildFromSortedRecords(IEnumerable<(byte[] Key, byte[] Record)> sortedItems)
    {
        // 1. Reset
        nodes.Clear();
        // Header node (0) always exists
        var headerNode = BTreeNode.CreateHeaderNode(Header, NodeSize);
        nodes.Add(headerNode);
        
        Header = Header with 
        { 
            TotalNodes = 1,
            FreeNodes = 0,
            LeafRecords = 0,
            TreeDepth = 0,
            RootNode = 0
        };

        // 2. State for bulk loading
        BTreeNode currentLeaf = BTreeNode.CreateLeafNode(1, NodeSize);
        uint currentLeafIndex = AllocateNode(currentLeaf);
        
        Header = Header with 
        { 
            FirstLeafNode = currentLeafIndex, 
            LastLeafNode = currentLeafIndex,
            TreeDepth = 1,
            RootNode = currentLeafIndex
        };

        // We track the "active" node at each index level (0 = leaf, 1 = first index layer, etc.)
        // But since we build bottom-up, we push "promoted" keys up.
        // Let's use a recursive "InsertIndex" approach or iterative layers.
        // Iterative layers: List<ActiveIndexNode>
        var indexLevels = new List<(BTreeNode Node, uint Index)>();

        foreach (var (key, record) in sortedItems)
        {
            // Check if fits in current leaf
            if (!currentLeaf.CanFit(record.Length))
            {
                // Verify leaf is not empty (if record > node size, we have a problem, but assuming it fits)
                if (currentLeaf.Records.Count == 0)
                    throw new InvalidOperationException("Record too large for node size");

                // Promote the FIRST key of the current leaf to the index above
                // Note: fsck_hfs error "Index key doesn't match first node key" confirms it expects the First Key (Lower Bound) logic.
                var keyToPromote = GetKeyFromRecord(currentLeaf.Records.First());
                PromoteKey(keyToPromote, currentLeafIndex, indexLevels);

                // Allocate new leaf
                var newLeaf = BTreeNode.CreateLeafNode(1, NodeSize);
                var newLeafIndex = AllocateNode(newLeaf);

                // Link
                currentLeaf.Descriptor = currentLeaf.Descriptor with { ForwardLink = newLeafIndex };
                newLeaf.Descriptor = newLeaf.Descriptor with { BackwardLink = currentLeafIndex };

                currentLeaf = newLeaf;
                currentLeafIndex = newLeafIndex;
                Header = Header with { LastLeafNode = currentLeafIndex };
            }

            currentLeaf.AddRecord(record);
            Header = Header with { LeafRecords = Header.LeafRecords + 1 };
        }

        // Finalize: Promote the last key of the last leaf?
        // No, HFS+ B-Trees use the key of the LAST record in the child as the key in the parent.
        // But for the *last* child, we usually don't need a separator if we just point to it?
        // Wait, every child pointer in an Index Node MUST have a key.
        // So yes, we must promote the key of the last leaf too.
        if (currentLeaf.Records.Count > 0)
        {
             // Consistently use First Key (Lower Bound) for the final node promotion as well.
             var keyToPromote = GetKeyFromRecord(currentLeaf.Records.First());
             
             // We only promote if we have index levels (depth > 1) or if we split.
             // If we never split (single leaf), indexLevels is empty, and we do nothing.
             if (indexLevels.Count > 0)
             {
                 PromoteKey(keyToPromote, currentLeafIndex, indexLevels);
             }
        }
        
        // Finalize index levels (link them up if they split? No, strictly bottom-up)
        // We might need to finish off active index nodes.
        // And update RootNode.
        if (indexLevels.Count > 0)
        {
             // The root is the highest level node
             var (rootNode, rootIndex) = indexLevels.Last();
             Header = Header with { RootNode = rootIndex, TreeDepth = (ushort)(indexLevels.Count + 1) };
        }

        UpdateHeaderNode();
    }

    private void PromoteKey(byte[] key, uint childNodeIndex, List<(BTreeNode Node, uint Index)> indexLevels)
    {
        // Build the pointer record: Key + NodeID (4 bytes)
        var pointerRecord = new byte[key.Length + 4];
        key.CopyTo(pointerRecord, 0);
        BigEndianWriter.WriteUInt32(pointerRecord.AsSpan(key.Length), childNodeIndex);

        // Try to insert each level up
        int level = 0;
        while (true)
        {
            if (level >= indexLevels.Count)
            {
                // Create new index level
                var newIndexNode = BTreeNode.CreateIndexNode((byte)(level + 2), NodeSize); // Height starts at 2 for first index level
                var newIndexNodeIndex = AllocateNode(newIndexNode);
                indexLevels.Add((newIndexNode, newIndexNodeIndex));
            }

            var (currentNode, currentIndex) = indexLevels[level];

            if (currentNode.CanFit(pointerRecord.Length))
            {
                currentNode.AddRecord(pointerRecord);
                break; // Done
            }
            else
            {
                // This index node is full. Split it.
                // Promote ITS last key to the next level.
                if (currentNode.Records.Count == 0)
                     throw new InvalidOperationException("Index record too large");
                
                // Key of the pointer record is just the Key part.
                var lastPointerRecord = currentNode.Records.Last();
                var lastKey = lastPointerRecord[..^4]; // Strip last 4 bytes (NodeID)

                PromoteKey(lastKey, currentIndex, indexLevels); // RECURSIVE call to next level (but we are inside that logic loop maybe?)
                // Wait, if we call PromoteKey recursively, it will try to add to level+1.
                // But we are iterating levels here?
                // Let's rely on recursion for simplicity instead of loop, or handle careful state.
                // Recursion is safer for "Promote to next level".
                // But we are in a loop for "levels".
                // Let's break the loop and recurse.
                
                // 1. Create new sibling index node
                var newSibling = BTreeNode.CreateIndexNode((byte)(level + 2), NodeSize);
                var newSiblingIndex = AllocateNode(newSibling);
                
                // 2. Link? Index nodes usually don't have sibling links in HFS+? 
                // Specs say "Index nodes: Flink and Blink are set to 0".
                // So no linkage needed.

                // 3. Update the list with the new active node for this level
                indexLevels[level] = (newSibling, newSiblingIndex);

                // 4. Recurse to add the "split key" to the parent
                // The key that represents the FULL `currentNode` is `lastKey`.
                // We add (`lastKey`, `currentIndex`) to `level + 1`.
                // NOTE: We already have `PromoteKey` handling this!
                // But we need to handle the infinite recursion risk?
                // `PromoteKey` adds to `level+1`.
                
                // Actually, `PromoteKey` is designed to add ONE item.
                // Logic:
                // We are trying to add `pointerRecord` to `level`.
                // `level` is full.
                // We seal `level` node (currentNode).
                // We verify its last key (which covers it).
                // We promote (`currentLastKey`, `currentIndex`) to `level+1`.
                // Then we replace `level` active node with new empty node.
                // Then we add `pointerRecord` to the NEW `level` node.
                
                // Correct Logic:
                // If we are strictly following "First Key" logic for Index Records:
                // The Index Node (currentNode) covers a range.
                // We are splitting it. 
                // The old node (currentNode) keeps the first half.
                // We create a new node (newNode) for the second half.
                // We need to promote a key for `currentNode`? NO.
                // We need to promote a key for `currentNode` up to `level + 1`.
                // If `level + 1` relies on "First Key", we should promote `currentNode.Records.First()`.
                // Existing `currentNode` is already represented in `level + 1` by its First Key (established earlier).
                // Wait, if we keep `currentNode` in place and add `newNode`, we just need to add `newNode` to `level + 1`.
                // And the Key for `newNode` should be ITS First Key.
                // But `newNode` is currently empty (about to receive `pointerRecord`).
                // So the First Key of `newNode` will be `pointerRecord.Key`.
                // So we promote `pointerRecord.Key` to `level + 1`.
                
                // Wait. `currentNode` is full. 
                // We keep `currentNode` as is? 
                // No, usually B-Trees split 50/50. 
                // But here I am doing a silly "Fill until full then new node" logic (100% fill).
                // So `currentNode` remains full.
                // We Create `newNode`.
                // We add `newNode` to `level + 1`. 
                // The Key for `newNode` in `level + 1` should be the First Key of `newNode`.
                // Since `newNode` starts empty, its first record will be `pointerRecord`.
                // So we promote `pointerRecord.Key` -> `level + 1`.
                
                // Check what `PromoteKey(prevKey, currentIndex, indexLevels)` was doing.
                // `currentIndex` is `newNode`? No `currentIndex` is `currentNode`.
                // `currentNode` is ALREADY in `level + 1`.
                // So we don't need to re-promote `currentNode`.
                // We need to promote `newNode`.
                // BUT `AllocateNode` for `newNode` gives `newNodeIndex`.
                // So we should call `PromoteKey(pointerRecord.Key, newNodeIndex, indexLevels)`.
                // Note: `PromoteKey` handles the "insert up".
                
                // Let's look at the old logic:
                // var prevKey = currentNode.Records.Last()[..^4];
                // PromoteKey(prevKey, currentIndex, indexLevels);
                
                // This old logic seems to be re-promoting the current node?
                // Or maybe updating the key for the current node behavior?
                // Standard B-Tree: Parent has keys [K1, K2] pointing to [N1, N2].
                // If N1 splits into N1, N3.
                // Parent becomes [K1, K3, K2].
                // But here we just append to the right (Fill 100%).
                // Parent has [K1]. Points to N1.
                // N1 fills. New N2.
                // Parent needs [K1, K2]. Points to N1, N2.
                // We need to add (K2, N2) to Parent.
                // K2 should be First Key of N2.
                // First Key of N2 is `pointerRecord.Key`.
                // So we promote (pointerRecord.Key, newNodeIndex).
                
                // FIX:
                var splitKey = pointerRecord[..^4]; // Key part of the record we are about to add
                
                // Create new node for THIS level
                var newNode = BTreeNode.CreateIndexNode((byte)(level + 2), NodeSize);
                var newNodeIndex = AllocateNode(newNode);
                indexLevels[level] = (newNode, newNodeIndex);
                
                // Recursively promote the new node to the parent level
                PromoteKey(splitKey, newNodeIndex, indexLevels);

                // Add the pending record to the new node
                newNode.AddRecord(pointerRecord);
                break; 
            }
        }
    }

    private byte[] GetKeyFromRecord(byte[] record)
    {
        // For leaf records, the key is at the start.
        // HFS+ Key format: KeyLength(2) + KeyData...
        if (record.Length < 2) return Array.Empty<byte>();
        var keyLen = BinaryPrimitives.ReadUInt16BigEndian(record.AsSpan(0, 2));
        // Total key size = 2 + keyLen
        if (record.Length < 2 + keyLen) return record; // Safety fallback
        return record[..(2 + keyLen)];
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
