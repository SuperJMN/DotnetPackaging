using DotnetPackaging.Hfs.BTree;

namespace DotnetPackaging.Hfs.Extents;

/// <summary>
/// HFS+ Extents Overflow B-Tree.
/// Stores extent records for files that need more than 8 extents per fork.
/// For simple volumes, this tree is typically empty.
/// </summary>
public sealed class ExtentsBTree
{
    private readonly BTreeFile btree;

    public ExtentsBTree(int nodeSize = 4096)
    {
        // Extent key: keyLength(2) + forkType(1) + pad(1) + fileID(4) + startBlock(4) = 12
        btree = new BTreeFile(nodeSize, maxKeyLength: 10);
    }

    /// <summary>
    /// Gets the underlying B-tree.
    /// </summary>
    public BTreeFile BTree => btree;

    /// <summary>
    /// Gets the total size in bytes.
    /// </summary>
    public int TotalSize => btree.TotalSize;

    /// <summary>
    /// Serializes to bytes.
    /// </summary>
    public byte[] ToBytes() => btree.ToBytes();

    /// <summary>
    /// Creates an IByteSource.
    /// </summary>
    public IByteSource ToByteSource() => btree.ToByteSource();
}
