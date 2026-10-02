using System.Buffers.Binary;

namespace DotnetPackaging.AppImage.Core;

/// <summary>
/// Writes a SquashFS metadata table: a sequence of blocks holding at most 8 KiB of
/// uncompressed data each, prefixed by a 16-bit header with the stored length.
/// </summary>
internal sealed class SquashFsMetadataWriter(Func<byte[], byte[]> compress)
{
    public const int BlockSize = 8192;
    private const ushort UncompressedFlag = 0x8000;

    private readonly MemoryStream output = new();
    private readonly MemoryStream pending = new();

    /// <summary>
    /// Position where the next byte will land: the start of the current block relative to
    /// the start of the table, and the offset inside that block once uncompressed.
    /// </summary>
    public SquashFsMetadataRef Position => new((uint)output.Length, (ushort)pending.Length);

    public void Write(ReadOnlySpan<byte> data)
    {
        while (data.Length > 0)
        {
            var chunk = Math.Min(BlockSize - (int)pending.Length, data.Length);
            pending.Write(data[..chunk]);
            data = data[chunk..];

            if (pending.Length == BlockSize)
            {
                FlushBlock();
            }
        }
    }

    public byte[] ToArray()
    {
        if (pending.Length > 0)
        {
            FlushBlock();
        }

        return output.ToArray();
    }

    private void FlushBlock()
    {
        var raw = pending.ToArray();
        var compressed = compress(raw);
        var useCompressed = compressed.Length < raw.Length;
        var stored = useCompressed ? compressed : raw;
        var header = (ushort)(stored.Length | (useCompressed ? 0 : UncompressedFlag));

        Span<byte> headerBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(headerBytes, header);
        output.Write(headerBytes);
        output.Write(stored);

        pending.SetLength(0);
    }
}

internal readonly record struct SquashFsMetadataRef(uint BlockStart, ushort Offset)
{
    public ulong Reference => ((ulong)BlockStart << 16) | Offset;
}
