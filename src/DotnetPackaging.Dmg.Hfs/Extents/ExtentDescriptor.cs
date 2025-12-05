using DotnetPackaging.Hfs.Encoding;

namespace DotnetPackaging.Hfs.Extents;

/// <summary>
/// HFS+ Extent Descriptor (8 bytes).
/// Describes a contiguous range of allocation blocks.
/// </summary>
public readonly record struct ExtentDescriptor(uint StartBlock, uint BlockCount)
{
    public const int Size = 8;

    public static readonly ExtentDescriptor Empty = new(0, 0);

    public bool IsEmpty => StartBlock == 0 && BlockCount == 0;

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        BigEndianWriter.WriteUInt32(buffer[..4], StartBlock);
        BigEndianWriter.WriteUInt32(buffer[4..8], BlockCount);
    }

    public static ExtentDescriptor FromBytes(ReadOnlySpan<byte> buffer)
    {
        var startBlock = BinaryPrimitives.ReadUInt32BigEndian(buffer[..4]);
        var blockCount = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..8]);
        return new ExtentDescriptor(startBlock, blockCount);
    }
}

/// <summary>
/// HFS+ Extent Record: 8 extent descriptors (64 bytes).
/// Used in catalog file records for the first 8 extents of a fork.
/// </summary>
public sealed record ExtentRecord
{
    public const int ExtentsPerRecord = 8;
    public const int Size = ExtentDescriptor.Size * ExtentsPerRecord; // 64 bytes

    public ExtentDescriptor[] Extents { get; init; } = CreateEmptyExtents();

    public static ExtentRecord Empty => new();

    private static ExtentDescriptor[] CreateEmptyExtents()
    {
        var extents = new ExtentDescriptor[ExtentsPerRecord];
        for (var i = 0; i < ExtentsPerRecord; i++)
            extents[i] = ExtentDescriptor.Empty;
        return extents;
    }

    public static ExtentRecord FromSingleExtent(uint startBlock, uint blockCount)
    {
        var extents = CreateEmptyExtents();
        extents[0] = new ExtentDescriptor(startBlock, blockCount);
        return new ExtentRecord { Extents = extents };
    }

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        for (var i = 0; i < ExtentsPerRecord; i++)
        {
            Extents[i].WriteTo(buffer[(i * ExtentDescriptor.Size)..]);
        }
    }
}

/// <summary>
/// HFS+ Fork Data (80 bytes).
/// Describes a file fork (data or resource), including size and first 8 extents.
/// </summary>
public sealed record ForkData
{
    public const int Size = 80;

    /// <summary>Logical size of the fork in bytes.</summary>
    public ulong LogicalSize { get; init; }

    /// <summary>Clump size (hint for growing the fork).</summary>
    public uint ClumpSize { get; init; }

    /// <summary>Total allocation blocks used by this fork.</summary>
    public uint TotalBlocks { get; init; }

    /// <summary>First 8 extents of the fork.</summary>
    public ExtentRecord Extents { get; init; } = ExtentRecord.Empty;

    public static ForkData Empty => new();

    public static ForkData FromExtent(ulong logicalSize, uint startBlock, uint blockCount)
    {
        return new ForkData
        {
            LogicalSize = logicalSize,
            TotalBlocks = blockCount,
            Extents = ExtentRecord.FromSingleExtent(startBlock, blockCount)
        };
    }

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        var offset = 0;

        // Logical size (8 bytes)
        BigEndianWriter.WriteUInt64(buffer[offset..], LogicalSize);
        offset += 8;

        // Clump size (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], ClumpSize);
        offset += 4;

        // Total blocks (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], TotalBlocks);
        offset += 4;

        // Extents (64 bytes)
        Extents.WriteTo(buffer[offset..]);
    }
}
