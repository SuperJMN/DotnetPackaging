using DotnetPackaging.Dmg.Hfs.Encoding;

namespace DotnetPackaging.Dmg.Hfs.BTree;

/// <summary>
/// B-Tree Header Record (106 bytes).
/// The first record in the header node (node 0) of every B-tree.
/// </summary>
public sealed record BTreeHeaderRecord
{
    public const int Size = 106;

    /// <summary>Current depth of the tree.</summary>
    public ushort TreeDepth { get; init; }

    /// <summary>Node number of the root node.</summary>
    public uint RootNode { get; init; }

    /// <summary>Number of leaf records in the tree.</summary>
    public uint LeafRecords { get; init; }

    /// <summary>Node number of the first leaf node.</summary>
    public uint FirstLeafNode { get; init; }

    /// <summary>Node number of the last leaf node.</summary>
    public uint LastLeafNode { get; init; }

    /// <summary>Size of each node in bytes.</summary>
    public ushort NodeSize { get; init; } = 4096;

    /// <summary>Maximum key length.</summary>
    public ushort MaxKeyLength { get; init; } = 516; // For catalog: 6 + 255*2

    /// <summary>Total number of nodes in the tree.</summary>
    public uint TotalNodes { get; init; }

    /// <summary>Number of free (unused) nodes.</summary>
    public uint FreeNodes { get; init; }

    /// <summary>Reserved (clump size, typically 0).</summary>
    public uint Reserved1 { get; init; }

    /// <summary>B-tree type (0 for HFS, 128/255 for HFS+).</summary>
    public BTreeType BTreeType { get; init; } = BTreeType.HfsPlus;

    /// <summary>Key comparison type.</summary>
    public KeyCompareType KeyCompareType { get; init; } = KeyCompareType.CaseFolding;

    /// <summary>B-tree attributes.</summary>
    public BTreeAttributes Attributes { get; init; } = BTreeAttributes.BigKeys | BTreeAttributes.VariableIndexKeys;

    /// <summary>Reserved (80 bytes, must be 0).</summary>
    public byte[] Reserved3 { get; init; } = new byte[64];

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        var offset = 0;

        BigEndianWriter.WriteUInt16(buffer[offset..], TreeDepth);
        offset += 2;

        BigEndianWriter.WriteUInt32(buffer[offset..], RootNode);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], LeafRecords);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], FirstLeafNode);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], LastLeafNode);
        offset += 4;

        BigEndianWriter.WriteUInt16(buffer[offset..], NodeSize);
        offset += 2;

        BigEndianWriter.WriteUInt16(buffer[offset..], MaxKeyLength);
        offset += 2;

        BigEndianWriter.WriteUInt32(buffer[offset..], TotalNodes);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], FreeNodes);
        offset += 4;

        BigEndianWriter.WriteUInt16(buffer[offset..], (ushort)Reserved1);
        offset += 2;

        buffer[offset++] = (byte)BTreeType;
        buffer[offset++] = (byte)KeyCompareType;

        BigEndianWriter.WriteUInt32(buffer[offset..], (uint)Attributes);
        offset += 4;

        // Reserved3 (64 bytes)
        Reserved3.AsSpan(0, Math.Min(64, Reserved3.Length)).CopyTo(buffer[offset..]);
    }
}

/// <summary>
/// B-tree type identifier.
/// </summary>
public enum BTreeType : byte
{
    /// <summary>HFS B-tree.</summary>
    Hfs = 0,
    
    /// <summary>HFS+ B-tree (user data).</summary>
    HfsPlus = 128,
    
    /// <summary>HFS+ B-tree (reserved).</summary>
    HfsPlusReserved = 255
}

/// <summary>
/// Key comparison type.
/// </summary>
public enum KeyCompareType : byte
{
    /// <summary>Case-folding comparison (default for HFS+).</summary>
    CaseFolding = 0xCF,
    
    /// <summary>Binary comparison (for HFSX case-sensitive).</summary>
    BinaryCompare = 0xBC
}

/// <summary>
/// B-tree attribute flags.
/// </summary>
[Flags]
public enum BTreeAttributes : uint
{
    None = 0,
    
    /// <summary>B-tree was closed incorrectly.</summary>
    BadClose = 1 << 0,
    
    /// <summary>Keys use 16-bit length field instead of 8-bit.</summary>
    BigKeys = 1 << 1,
    
    /// <summary>Index nodes have variable-size keys.</summary>
    VariableIndexKeys = 1 << 2
}
