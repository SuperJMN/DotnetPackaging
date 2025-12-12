using DotnetPackaging.Dmg.Hfs.Encoding;
using DotnetPackaging.Dmg.Hfs.Extents;

namespace DotnetPackaging.Dmg.Hfs.Catalog;

/// <summary>
/// Catalog record type identifiers.
/// </summary>
public enum CatalogRecordType : short
{
    Folder = 0x0001,
    File = 0x0002,
    FolderThread = 0x0003,
    FileThread = 0x0004
}

/// <summary>
/// BSD file info (permissions, owner, group) - 16 bytes.
/// </summary>
public sealed record BsdInfo
{
    public const int Size = 16;

    public uint OwnerId { get; init; } = 99;    // nobody
    public uint GroupId { get; init; } = 99;   // nobody
    public byte AdminFlags { get; init; }
    public byte OwnerFlags { get; init; }
    public ushort FileMode { get; init; } = 0x81ED; // -rwxr-xr-x (regular file, 0755)
    public uint Special { get; init; }  // device ID or link count

    public static BsdInfo ForDirectory() => new()
    {
        FileMode = 0x41ED // drwxr-xr-x (directory, 0755)
    };

    public static BsdInfo ForFile() => new()
    {
        FileMode = 0x81ED // -rwxr-xr-x (regular file, 0755)
    };

    public static BsdInfo ForSymlink() => new()
    {
        FileMode = 0xA1ED // lrwxr-xr-x (symlink, 0755)
    };

    public void WriteTo(Span<byte> buffer)
    {
        BigEndianWriter.WriteUInt32(buffer[0..4], OwnerId);
        BigEndianWriter.WriteUInt32(buffer[4..8], GroupId);
        buffer[8] = AdminFlags;
        buffer[9] = OwnerFlags;
        BigEndianWriter.WriteUInt16(buffer[10..12], FileMode);
        BigEndianWriter.WriteUInt32(buffer[12..16], Special);
    }
}

/// <summary>
/// Finder info for files - 16 bytes.
/// </summary>
public sealed record FileFinderInfo
{
    public const int Size = 16;

    public uint FileType { get; init; }        // e.g., 'TEXT'
    public uint FileCreator { get; init; }     // e.g., 'ttxt'
    public ushort FinderFlags { get; init; }
    public short LocationV { get; init; }
    public short LocationH { get; init; }
    public ushort ReservedField { get; init; }

    public void WriteTo(Span<byte> buffer)
    {
        BigEndianWriter.WriteUInt32(buffer[0..4], FileType);
        BigEndianWriter.WriteUInt32(buffer[4..8], FileCreator);
        BigEndianWriter.WriteUInt16(buffer[8..10], FinderFlags);
        BigEndianWriter.WriteInt16(buffer[10..12], LocationV);
        BigEndianWriter.WriteInt16(buffer[12..14], LocationH);
        BigEndianWriter.WriteUInt16(buffer[14..16], ReservedField);
    }
}

/// <summary>
/// Extended Finder info - 16 bytes.
/// </summary>
public sealed record ExtendedFinderInfo
{
    public const int Size = 16;

    public short Reserved1 { get; init; }
    public short Reserved2 { get; init; }
    public int Reserved3 { get; init; }
    public short Reserved4 { get; init; }
    public short Reserved5 { get; init; }
    public int Reserved6 { get; init; }

    public void WriteTo(Span<byte> buffer)
    {
        buffer[..Size].Clear();
    }
}

/// <summary>
/// Finder info for folders - 16 bytes.
/// </summary>
public sealed record FolderFinderInfo
{
    public const int Size = 16;

    public short WindowTop { get; init; }
    public short WindowLeft { get; init; }
    public short WindowBottom { get; init; }
    public short WindowRight { get; init; }
    public ushort FinderFlags { get; init; }
    public short LocationV { get; init; }
    public short LocationH { get; init; }
    public ushort ReservedField { get; init; }

    public void WriteTo(Span<byte> buffer)
    {
        BigEndianWriter.WriteInt16(buffer[0..2], WindowTop);
        BigEndianWriter.WriteInt16(buffer[2..4], WindowLeft);
        BigEndianWriter.WriteInt16(buffer[4..6], WindowBottom);
        BigEndianWriter.WriteInt16(buffer[6..8], WindowRight);
        BigEndianWriter.WriteUInt16(buffer[8..10], FinderFlags);
        BigEndianWriter.WriteInt16(buffer[10..12], LocationV);
        BigEndianWriter.WriteInt16(buffer[12..14], LocationH);
        BigEndianWriter.WriteUInt16(buffer[14..16], ReservedField);
    }
}

/// <summary>
/// HFS+ Catalog Folder Record (88 bytes).
/// </summary>
public sealed record CatalogFolderRecord
{
    public const int Size = 88;

    public CatalogRecordType RecordType { get; init; } = CatalogRecordType.Folder;
    public ushort Flags { get; init; }
    public uint Valence { get; init; }        // Number of items in folder
    public CatalogNodeId FolderId { get; init; }
    public DateTime CreateDate { get; init; } = DateTime.UtcNow;
    public DateTime ContentModDate { get; init; } = DateTime.UtcNow;
    public DateTime AttributeModDate { get; init; } = DateTime.UtcNow;
    public DateTime AccessDate { get; init; } = DateTime.UtcNow;
    public DateTime BackupDate { get; init; }
    public BsdInfo Permissions { get; init; } = BsdInfo.ForDirectory();
    public FolderFinderInfo FinderInfo { get; init; } = new();
    public ExtendedFinderInfo ExtendedFinderInfo { get; init; } = new();
    public uint TextEncoding { get; init; }
    public uint Reserved { get; init; }

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        var offset = 0;

        BigEndianWriter.WriteInt16(buffer[offset..], (short)RecordType);
        offset += 2;

        BigEndianWriter.WriteUInt16(buffer[offset..], Flags);
        offset += 2;

        BigEndianWriter.WriteUInt32(buffer[offset..], Valence);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], FolderId.Value);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], CreateDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], ContentModDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], AttributeModDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], AccessDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], BackupDate);
        offset += 4;

        Permissions.WriteTo(buffer[offset..]);
        offset += BsdInfo.Size;

        FinderInfo.WriteTo(buffer[offset..]);
        offset += FolderFinderInfo.Size;

        ExtendedFinderInfo.WriteTo(buffer[offset..]);
        offset += ExtendedFinderInfo.Size;

        BigEndianWriter.WriteUInt32(buffer[offset..], TextEncoding);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], Reserved);
    }
}

/// <summary>
/// HFS+ Catalog File Record (248 bytes).
/// </summary>
public sealed record CatalogFileRecord
{
    public const int Size = 248;

    public CatalogRecordType RecordType { get; init; } = CatalogRecordType.File;
    public ushort Flags { get; init; }
    public uint Reserved1 { get; init; }
    public CatalogNodeId FileId { get; init; }
    public DateTime CreateDate { get; init; } = DateTime.UtcNow;
    public DateTime ContentModDate { get; init; } = DateTime.UtcNow;
    public DateTime AttributeModDate { get; init; } = DateTime.UtcNow;
    public DateTime AccessDate { get; init; } = DateTime.UtcNow;
    public DateTime BackupDate { get; init; }
    public BsdInfo Permissions { get; init; } = BsdInfo.ForFile();
    public FileFinderInfo FinderInfo { get; init; } = new();
    public ExtendedFinderInfo ExtendedFinderInfo { get; init; } = new();
    public uint TextEncoding { get; init; }
    public uint Reserved2 { get; init; }
    public ForkData DataFork { get; init; } = ForkData.Empty;
    public ForkData ResourceFork { get; init; } = ForkData.Empty;

    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        var offset = 0;

        BigEndianWriter.WriteInt16(buffer[offset..], (short)RecordType);
        offset += 2;

        BigEndianWriter.WriteUInt16(buffer[offset..], Flags);
        offset += 2;

        BigEndianWriter.WriteUInt32(buffer[offset..], Reserved1);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], FileId.Value);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], CreateDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], ContentModDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], AttributeModDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], AccessDate);
        offset += 4;

        HfsDateTime.WriteTimestamp(buffer[offset..], BackupDate);
        offset += 4;

        Permissions.WriteTo(buffer[offset..]);
        offset += BsdInfo.Size;

        FinderInfo.WriteTo(buffer[offset..]);
        offset += FileFinderInfo.Size;

        ExtendedFinderInfo.WriteTo(buffer[offset..]);
        offset += ExtendedFinderInfo.Size;

        BigEndianWriter.WriteUInt32(buffer[offset..], TextEncoding);
        offset += 4;

        BigEndianWriter.WriteUInt32(buffer[offset..], Reserved2);
        offset += 4;

        DataFork.WriteTo(buffer[offset..]);
        offset += ForkData.Size;

        ResourceFork.WriteTo(buffer[offset..]);
    }

    /// <summary>
    /// Creates a file record for a symlink.
    /// </summary>
    public static CatalogFileRecord ForSymlink(CatalogNodeId fileId, string target)
    {
        // Symlinks store the target path in the data fork
        var targetBytes = System.Text.Encoding.UTF8.GetBytes(target);
        
        return new CatalogFileRecord
        {
            FileId = fileId,
            Flags = 0,
            Permissions = BsdInfo.ForSymlink(),
            DataFork = new ForkData
            {
                LogicalSize = (ulong)targetBytes.Length
            },
            FinderInfo = new FileFinderInfo
            {
                FileType = 0x736C6E6B, // 'slnk'
                FileCreator = 0x72686170 // 'rhap'
            }
        };
    }
}

/// <summary>
/// HFS+ Catalog Thread Record (variable size, min 10 bytes).
/// Used to find the parent of a file/folder given its CNID.
/// </summary>
public sealed record CatalogThreadRecord
{
    public const int MinSize = 10;

    public CatalogRecordType RecordType { get; init; }
    public short Reserved { get; init; }
    public CatalogNodeId ParentId { get; init; }
    public string NodeName { get; init; } = string.Empty;

    public static CatalogThreadRecord ForFolder(CatalogNodeId parentId, string name)
        => new() { RecordType = CatalogRecordType.FolderThread, ParentId = parentId, NodeName = name };

    public static CatalogThreadRecord ForFile(CatalogNodeId parentId, string name)
        => new() { RecordType = CatalogRecordType.FileThread, ParentId = parentId, NodeName = name };

    public int Size => MinSize + HfsUnicode.GetByteLength(NodeName);

    public byte[] ToBytes()
    {
        var nameWithLength = HfsUnicode.EncodeWithLength(NodeName);
        // Header (8 bytes) + NameWithLength
        var buffer = new byte[8 + nameWithLength.Length];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        BigEndianWriter.WriteInt16(buffer[0..2], (short)RecordType);
        BigEndianWriter.WriteInt16(buffer[2..4], Reserved);
        BigEndianWriter.WriteUInt32(buffer[4..8], ParentId.Value);
        
        var nameWithLength = HfsUnicode.EncodeWithLength(NodeName);
        nameWithLength.CopyTo(buffer[8..]);
    }
}
