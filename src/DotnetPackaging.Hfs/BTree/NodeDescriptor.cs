using DotnetPackaging.Hfs.Encoding;

namespace DotnetPackaging.Hfs.BTree;

/// <summary>
/// B-Tree Node Descriptor (14 bytes).
/// Every node in a B-tree begins with this descriptor.
/// </summary>
public sealed record NodeDescriptor
{
    public const int Size = 14;

    /// <summary>Forward link to next node at same level (0 if none).</summary>
    public uint ForwardLink { get; init; }

    /// <summary>Backward link to previous node at same level (0 if none).</summary>
    public uint BackwardLink { get; init; }

    /// <summary>Type of this node.</summary>
    public NodeKind Kind { get; init; }

    /// <summary>Depth of this node in the tree (leaves are at depth 1).</summary>
    public byte Height { get; init; }

    /// <summary>Number of records in this node.</summary>
    public ushort NumRecords { get; init; }

    /// <summary>Reserved (must be 0).</summary>
    public ushort Reserved { get; init; }

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        BigEndianWriter.WriteUInt32(buffer[0..4], ForwardLink);
        BigEndianWriter.WriteUInt32(buffer[4..8], BackwardLink);
        buffer[8] = (byte)Kind;
        buffer[9] = Height;
        BigEndianWriter.WriteUInt16(buffer[10..12], NumRecords);
        BigEndianWriter.WriteUInt16(buffer[12..14], Reserved);
    }

    public static NodeDescriptor FromBytes(ReadOnlySpan<byte> buffer)
    {
        return new NodeDescriptor
        {
            ForwardLink = BinaryPrimitives.ReadUInt32BigEndian(buffer[0..4]),
            BackwardLink = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..8]),
            Kind = (NodeKind)buffer[8],
            Height = buffer[9],
            NumRecords = BinaryPrimitives.ReadUInt16BigEndian(buffer[10..12]),
            Reserved = BinaryPrimitives.ReadUInt16BigEndian(buffer[12..14])
        };
    }
}

/// <summary>
/// B-Tree node types.
/// </summary>
public enum NodeKind : byte
{
    /// <summary>Leaf node containing actual records.</summary>
    Leaf = 0xFF,
    
    /// <summary>Index node containing pointers to child nodes.</summary>
    Index = 0x00,
    
    /// <summary>Header node (always node 0).</summary>
    Header = 0x01,
    
    /// <summary>Map node containing node allocation bitmap.</summary>
    Map = 0x02
}
