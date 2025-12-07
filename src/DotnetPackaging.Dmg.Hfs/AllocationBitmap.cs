namespace DotnetPackaging.Hfs;

/// <summary>
/// HFS+ Allocation File.
/// A bitmap where each bit represents one allocation block.
/// 0 = free, 1 = used.
/// </summary>
public sealed class AllocationBitmap
{
    private readonly byte[] bitmap;
    private readonly uint totalBlocks;
    private uint usedBlocks;

    public AllocationBitmap(uint totalBlocks)
    {
        this.totalBlocks = totalBlocks;
        var byteCount = (totalBlocks + 7) / 8;
        bitmap = new byte[byteCount];
    }

    /// <summary>
    /// Total number of allocation blocks.
    /// </summary>
    public uint TotalBlocks => totalBlocks;

    /// <summary>
    /// Number of used blocks.
    /// </summary>
    public uint UsedBlocks => usedBlocks;

    /// <summary>
    /// Number of free blocks.
    /// </summary>
    public uint FreeBlocks => totalBlocks - usedBlocks;

    /// <summary>
    /// Size of the bitmap in bytes.
    /// </summary>
    public int Size => bitmap.Length;

    /// <summary>
    /// Marks a single block as used.
    /// </summary>
    public void MarkUsed(uint blockIndex)
    {
        if (blockIndex >= totalBlocks)
            throw new ArgumentOutOfRangeException(nameof(blockIndex));

        var byteIndex = blockIndex / 8;
        var bitIndex = 7 - (int)(blockIndex % 8); // Big-endian bit order
        
        if ((bitmap[byteIndex] & (1 << bitIndex)) == 0)
        {
            bitmap[byteIndex] |= (byte)(1 << bitIndex);
            usedBlocks++;
        }
    }

    /// <summary>
    /// Marks a range of blocks as used.
    /// </summary>
    public void MarkUsed(uint startBlock, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            MarkUsed(startBlock + i);
        }
    }

    /// <summary>
    /// Allocates a contiguous range of blocks and returns the start block.
    /// Returns null if not enough contiguous free blocks are available.
    /// </summary>
    public uint? Allocate(uint count, uint startHint = 0)
    {
        if (count == 0) return 0;
        
        uint consecutive = 0;
        uint startBlock = 0;

        for (uint i = startHint; i < totalBlocks; i++)
        {
            if (IsFree(i))
            {
                if (consecutive == 0)
                    startBlock = i;
                
                consecutive++;
                
                if (consecutive >= count)
                {
                    MarkUsed(startBlock, count);
                    return startBlock;
                }
            }
            else
            {
                consecutive = 0;
            }
        }
        
        // If not found from startHint, try from 0 if startHint > 0?
        // For our rigorous linear Writer, we usually want strictly forward, but generic bitmap should scan all.
        // But for now, strict forward is fine for the Writer's purpose.
        
        return null;
    }

    /// <summary>
    /// Checks if a block is free.
    /// </summary>
    public bool IsFree(uint blockIndex)
    {
        if (blockIndex >= totalBlocks) return false;

        var byteIndex = blockIndex / 8;
        var bitIndex = 7 - (int)(blockIndex % 8);
        return (bitmap[byteIndex] & (1 << bitIndex)) == 0;
    }

    /// <summary>
    /// Gets the bitmap bytes.
    /// </summary>
    public byte[] ToBytes() => (byte[])bitmap.Clone();

    /// <summary>
    /// Creates an IByteSource from this bitmap.
    /// </summary>
    public IByteSource ToByteSource() => ByteSource.FromBytes(bitmap);
}
