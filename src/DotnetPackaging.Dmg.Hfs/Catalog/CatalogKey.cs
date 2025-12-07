using DotnetPackaging.Hfs.Encoding;

namespace DotnetPackaging.Hfs.Catalog;

/// <summary>
/// HFS+ Catalog Key.
/// Consists of:
/// - keyLength (2 bytes): total key length minus 2
/// - parentID (4 bytes): CNID of parent folder
/// - nodeName (variable): HFS+ Unicode string with length prefix
/// </summary>
public sealed record CatalogKey
{
    /// <summary>Minimum key size: keyLength(2) + parentID(4) + nameLength(2) = 8</summary>
    public const int MinSize = 8;

    /// <summary>CNID of the parent folder.</summary>
    public CatalogNodeId ParentId { get; init; }

    /// <summary>Name of the file or folder.</summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// Creates a key for the root folder (parent=1, name=VolumeName).
    /// </summary>
    public static CatalogKey ForRootFolder(string volumeName)
        => new() { ParentId = CatalogNodeId.RootParent, NodeName = volumeName };

    /// <summary>
    /// Creates a key for a thread record (parent=CNID, name="").
    /// </summary>
    public static CatalogKey ForThread(CatalogNodeId cnid)
        => new() { ParentId = cnid, NodeName = string.Empty };

    /// <summary>
    /// Creates a key for a file or folder.
    /// </summary>
    public static CatalogKey For(CatalogNodeId parentId, string name)
        => new() { ParentId = parentId, NodeName = name };

    /// <summary>
    /// Gets the total serialized size of this key.
    /// </summary>
    public int Size
    {
        get
        {
            var nameBytes = HfsUnicode.GetByteLengthWithPrefix(NodeName);
            return 2 + 4 + nameBytes; // keyLength + parentID + nodeName
        }
    }

    /// <summary>
    /// Serializes the key to bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        var nameWithLength = HfsUnicode.EncodeWithLength(NodeName);
        var totalSize = 2 + 4 + nameWithLength.Length;
        var keyLength = (ushort)(4 + nameWithLength.Length); // excludes keyLength field itself

        var buffer = new byte[totalSize];
        BigEndianWriter.WriteUInt16(buffer.AsSpan(0, 2), keyLength);
        BigEndianWriter.WriteUInt32(buffer.AsSpan(2, 4), ParentId.Value);
        nameWithLength.CopyTo(buffer, 6);

        return buffer;
    }

    /// <summary>
    /// Writes the key to a span. Returns bytes written.
    /// </summary>
    public int WriteTo(Span<byte> buffer)
    {
        var nameWithLength = HfsUnicode.EncodeWithLength(NodeName);
        var keyLength = (ushort)(4 + nameWithLength.Length);

        BigEndianWriter.WriteUInt16(buffer[..2], keyLength);
        BigEndianWriter.WriteUInt32(buffer[2..6], ParentId.Value);
        nameWithLength.CopyTo(buffer[6..]);

        return 2 + 4 + nameWithLength.Length;
    }

    /// <summary>
    /// Compares two catalog keys for ordering in the B-tree.
    /// Keys are ordered by (parentID, nodeName) using case-insensitive comparison.
    /// </summary>
    public static int Compare(CatalogKey a, CatalogKey b)
    {
        var parentCompare = a.ParentId.Value.CompareTo(b.ParentId.Value);
        if (parentCompare != 0)
            return parentCompare;

        return HfsUnicode.Compare(a.NodeName, b.NodeName);
    }
}
