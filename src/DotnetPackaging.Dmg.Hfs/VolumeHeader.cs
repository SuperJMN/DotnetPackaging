using DotnetPackaging.Hfs.Encoding;
using DotnetPackaging.Hfs.Extents;

namespace DotnetPackaging.Hfs;

/// <summary>
/// HFS+ Volume Header (512 bytes).
/// Located at sector 2 (offset 1024) and duplicated at the second-to-last sector.
/// All multi-byte values are big-endian.
/// </summary>
public sealed record VolumeHeader
{
    public const int Size = 512;
    public const ushort HfsPlusSignature = 0x482B; // 'H+'
    public const ushort HfsxSignature = 0x4858;    // 'HX'
    public const ushort CurrentVersion = 4;        // HFS+ version 4
    public const ushort HfsxVersion = 5;           // HFSX version

    /// <summary>Signature: 'H+' (0x482B) for HFS+, 'HX' (0x4858) for HFSX.</summary>
    public ushort Signature { get; init; } = HfsPlusSignature;

    /// <summary>Version number (4 for HFS+, 5 for HFSX).</summary>
    public ushort Version { get; init; } = CurrentVersion;

    /// <summary>Volume attributes flags.</summary>
    public VolumeAttributes Attributes { get; init; } = VolumeAttributes.Unmounted;

    /// <summary>Last mounted version signature (typically '10.0' or 'HFSJ').</summary>
    public uint LastMountedVersion { get; init; } = 0x3130302E; // '10.0'

    /// <summary>Journal info block location (0 if not journaled).</summary>
    public uint JournalInfoBlock { get; init; }

    /// <summary>Date and time of volume creation.</summary>
    public DateTime CreateDate { get; init; } = DateTime.UtcNow;

    /// <summary>Date and time of last modification.</summary>
    public DateTime ModifyDate { get; init; } = DateTime.UtcNow;

    /// <summary>Date and time of last backup.</summary>
    public DateTime BackupDate { get; init; }

    /// <summary>Date and time of last consistency check.</summary>
    public DateTime CheckedDate { get; init; } = DateTime.UtcNow;

    /// <summary>Total number of files on the volume.</summary>
    public uint FileCount { get; init; }

    /// <summary>Total number of folders on the volume.</summary>
    public uint FolderCount { get; init; }

    /// <summary>Size of each allocation block in bytes (typically 4096).</summary>
    public uint BlockSize { get; init; } = 4096;

    /// <summary>Total number of allocation blocks on the volume.</summary>
    public uint TotalBlocks { get; init; }

    /// <summary>Number of free allocation blocks.</summary>
    public uint FreeBlocks { get; init; }

    /// <summary>First allocation block in the volume.</summary>
    public uint NextAllocation { get; init; }

    /// <summary>Size of resource clumps (hint for file growth).</summary>
    public uint ResourceClumpSize { get; init; } = 65536;

    /// <summary>Size of data clumps (hint for file growth).</summary>
    public uint DataClumpSize { get; init; } = 65536;

    /// <summary>Next unused catalog node ID.</summary>
    public uint NextCatalogId { get; init; } = 16; // First 16 CNIDs are reserved

    /// <summary>Number of times the volume has been written.</summary>
    public uint WriteCount { get; init; } = 1;

    /// <summary>Bitmap of used special file encodings.</summary>
    public ulong EncodingsBitmap { get; init; }

    /// <summary>Finder info (32 bytes, used by Finder for volume settings).</summary>
    public byte[] FinderInfo { get; init; } = new byte[32];

    /// <summary>Fork data for the allocation file.</summary>
    public ForkData AllocationFile { get; init; } = ForkData.Empty;

    /// <summary>Fork data for the extents overflow file.</summary>
    public ForkData ExtentsFile { get; init; } = ForkData.Empty;

    /// <summary>Fork data for the catalog file.</summary>
    public ForkData CatalogFile { get; init; } = ForkData.Empty;

    /// <summary>Fork data for the attributes file.</summary>
    public ForkData AttributesFile { get; init; } = ForkData.Empty;

    /// <summary>Fork data for the startup file.</summary>
    public ForkData StartupFile { get; init; } = ForkData.Empty;

    /// <summary>
    /// Serializes the volume header to exactly 512 bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    /// <summary>
    /// Writes the volume header to a span.
    /// </summary>
    public void WriteTo(Span<byte> buffer)
    {
        var offset = 0;

        // Signature (2 bytes)
        BigEndianWriter.WriteUInt16(buffer[offset..], Signature);
        offset += 2;

        // Version (2 bytes)
        BigEndianWriter.WriteUInt16(buffer[offset..], Version);
        offset += 2;

        // Attributes (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], (uint)Attributes);
        offset += 4;

        // Last mounted version (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], LastMountedVersion);
        offset += 4;

        // Journal info block (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], JournalInfoBlock);
        offset += 4;

        // Timestamps (4 bytes each = 16 bytes)
        HfsDateTime.WriteTimestamp(buffer[offset..], CreateDate);
        offset += 4;
        HfsDateTime.WriteTimestamp(buffer[offset..], ModifyDate);
        offset += 4;
        HfsDateTime.WriteTimestamp(buffer[offset..], BackupDate);
        offset += 4;
        HfsDateTime.WriteTimestamp(buffer[offset..], CheckedDate);
        offset += 4;

        // File count (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], FileCount);
        offset += 4;

        // Folder count (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], FolderCount);
        offset += 4;

        // Block size (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], BlockSize);
        offset += 4;

        // Total blocks (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], TotalBlocks);
        offset += 4;

        // Free blocks (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], FreeBlocks);
        offset += 4;

        // Next allocation (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], NextAllocation);
        offset += 4;

        // Resource clump size (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], ResourceClumpSize);
        offset += 4;

        // Data clump size (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], DataClumpSize);
        offset += 4;

        // Next catalog ID (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], NextCatalogId);
        offset += 4;

        // Write count (4 bytes)
        BigEndianWriter.WriteUInt32(buffer[offset..], WriteCount);
        offset += 4;

        // Encodings bitmap (8 bytes)
        BigEndianWriter.WriteUInt64(buffer[offset..], EncodingsBitmap);
        offset += 8;

        // Finder info (32 bytes)
        FinderInfo.AsSpan(0, Math.Min(32, FinderInfo.Length)).CopyTo(buffer[offset..]);
        offset += 32;

        // Fork data for special files (80 bytes each = 400 bytes)
        AllocationFile.WriteTo(buffer[offset..]);
        offset += ForkData.Size;

        ExtentsFile.WriteTo(buffer[offset..]);
        offset += ForkData.Size;

        CatalogFile.WriteTo(buffer[offset..]);
        offset += ForkData.Size;

        AttributesFile.WriteTo(buffer[offset..]);
        offset += ForkData.Size;

        StartupFile.WriteTo(buffer[offset..]);
        // offset += ForkData.Size; // Last one, no need to increment
    }
}

/// <summary>
/// Volume attribute flags.
/// </summary>
[Flags]
public enum VolumeAttributes : uint
{
    None = 0,
    
    /// <summary>Volume hardware lock.</summary>
    HardwareLock = 1 << 7,
    
    /// <summary>Volume was properly unmounted.</summary>
    Unmounted = 1 << 8,
    
    /// <summary>Volume has bad blocks.</summary>
    SparedBlocks = 1 << 9,
    
    /// <summary>Volume should not be cached.</summary>
    NoCacheRequired = 1 << 10,
    
    /// <summary>Boot volume is inconsistent.</summary>
    BootVolumeInconsistent = 1 << 11,
    
    /// <summary>Catalog node IDs reused.</summary>
    CatalogNodeIdsReused = 1 << 12,
    
    /// <summary>Volume is journaled.</summary>
    Journaled = 1 << 13,
    
    /// <summary>Volume is software locked.</summary>
    SoftwareLock = 1 << 15
}
