using DotnetPackaging.Hfs.BTree;

namespace DotnetPackaging.Hfs.Attributes;

/// <summary>
/// HFS+ Attributes B-Tree.
/// Stores extended attributes for files and folders.
/// For simple volumes, this tree may be empty.
/// </summary>
public sealed class AttributesBTree
{
    private readonly BTreeFile btree;

    public AttributesBTree(int nodeSize = 4096)
    {
        // Attribute key max length
        btree = new BTreeFile(nodeSize, maxKeyLength: 532);
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
