using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace DotnetPackaging.AppImage.Core;

/// <summary>
/// Builds gzip-compressed SquashFS 4.0 images that both squashfuse (used by the AppImage runtime)
/// and the Linux kernel driver (used by loop mounts, e.g. Firejail's --appimage) accept.
/// </summary>
/// <remarks>
/// File tails are not packed into fragments and xattrs are not stored. The image is padded to
/// 4 KiB because loop devices round their size down to whole sectors and the kernel rejects
/// images whose recorded size exceeds the device.
/// </remarks>
internal sealed class SquashFsImage
{
    public const int BlockSize = 128 * 1024;
    public const int DevicePadding = 4096;

    private const ushort BlockLog = 17;
    private const int SuperblockSize = 96;
    private const uint Magic = 0x73717368;
    private const ushort GzipCompressor = 1;
    private const ushort NoFragmentsFlag = 0x0010;
    private const ushort NoXattrsFlag = 0x0200;
    private const uint UncompressedDataBlockFlag = 1 << 24;
    private const uint NoFragment = uint.MaxValue;
    private const uint NoXattr = uint.MaxValue;
    private const ulong NotPresent = ulong.MaxValue;
    private const int MaxNameLength = 256;
    private const int MaxEntriesPerHeader = 256;
    private const int IdsPerMetadataBlock = SquashFsMetadataWriter.BlockSize / sizeof(uint);

    private const ushort DirectoryType = 1;
    private const ushort FileType = 2;
    private const ushort ExtendedDirectoryType = 8;
    private const ushort ExtendedFileType = 9;
    private const ushort PermissionBits = 0x0FFF;

    private static readonly Comparer<byte[]> NameOrder = Comparer<byte[]>.Create((a, b) => a.AsSpan().SequenceCompareTo(b));

    private readonly MemoryStream image = new();
    private readonly SquashFsMetadataWriter inodes = new(Compress);
    private readonly SquashFsMetadataWriter directories = new(Compress);
    private readonly Dictionary<SquashFsNode, uint> inodeNumbers = new(ReferenceEqualityComparer.Instance);
    private readonly List<uint> ids = new();
    private readonly uint modificationTime;

    private SquashFsImage(DateTimeOffset modificationTime)
    {
        this.modificationTime = (uint)Math.Clamp(modificationTime.ToUnixTimeSeconds(), 0, uint.MaxValue);
    }

    public static byte[] Create(SquashFsDirectory root, DateTimeOffset modificationTime)
    {
        return new SquashFsImage(modificationTime).Build(root);
    }

    private byte[] Build(SquashFsDirectory root)
    {
        NumberInodes(root);

        image.Write(new byte[SuperblockSize]);
        var rootInode = WriteDirectory(root, parentInodeNumber: (uint)inodeNumbers.Count + 1);

        var inodeTableStart = (ulong)image.Position;
        image.Write(inodes.ToArray());

        var directoryTableStart = (ulong)image.Position;
        image.Write(directories.ToArray());

        var idTableStart = WriteIdTable();
        var bytesUsed = (ulong)image.Length;

        WriteSuperblock(rootInode, bytesUsed, idTableStart, inodeTableStart, directoryTableStart);
        PadToDeviceSize();

        return image.ToArray();
    }

    /// <summary>
    /// Children of a directory get consecutive numbers so directory headers can address them
    /// with small deltas.
    /// </summary>
    private void NumberInodes(SquashFsDirectory root)
    {
        var next = 1u;
        inodeNumbers[root] = next++;

        var pending = new Queue<SquashFsDirectory>([root]);
        while (pending.TryDequeue(out var directory))
        {
            foreach (var (child, _) in SortedChildren(directory))
            {
                inodeNumbers[child] = next++;
                if (child is SquashFsDirectory subdirectory)
                {
                    pending.Enqueue(subdirectory);
                }
            }
        }
    }

    private SquashFsMetadataRef WriteDirectory(SquashFsDirectory directory, uint parentInodeNumber)
    {
        var entries = SortedChildren(directory)
            .Select(child => child.Node switch
            {
                SquashFsDirectory subdirectory => new DirectoryEntry(child.Name, WriteDirectory(subdirectory, inodeNumbers[directory]), inodeNumbers[subdirectory], DirectoryType),
                SquashFsFile file => new DirectoryEntry(child.Name, WriteFile(file), inodeNumbers[file], FileType),
                _ => throw new InvalidOperationException($"Unsupported SquashFS node '{child.Node.Name}'")
            })
            .ToList();

        var listing = BuildListing(entries);
        var listingPosition = directories.Position;
        directories.Write(listing);

        // The stored size counts the implicit "." and ".." entries as 3 bytes.
        var fileSize = (uint)listing.Length + 3;
        var linkCount = 2u + (uint)directory.Children.OfType<SquashFsDirectory>().Count();
        var position = inodes.Position;

        if (fileSize <= ushort.MaxValue)
        {
            var inode = InodeHeader(DirectoryType, directory, 16);
            var body = inode.AsSpan(16);
            BinaryPrimitives.WriteUInt32LittleEndian(body[0..], listingPosition.BlockStart);
            BinaryPrimitives.WriteUInt32LittleEndian(body[4..], linkCount);
            BinaryPrimitives.WriteUInt16LittleEndian(body[8..], (ushort)fileSize);
            BinaryPrimitives.WriteUInt16LittleEndian(body[10..], listingPosition.Offset);
            BinaryPrimitives.WriteUInt32LittleEndian(body[12..], parentInodeNumber);
            inodes.Write(inode);
        }
        else
        {
            var inode = InodeHeader(ExtendedDirectoryType, directory, 24);
            var body = inode.AsSpan(16);
            BinaryPrimitives.WriteUInt32LittleEndian(body[0..], linkCount);
            BinaryPrimitives.WriteUInt32LittleEndian(body[4..], fileSize);
            BinaryPrimitives.WriteUInt32LittleEndian(body[8..], listingPosition.BlockStart);
            BinaryPrimitives.WriteUInt32LittleEndian(body[12..], parentInodeNumber);
            BinaryPrimitives.WriteUInt16LittleEndian(body[16..], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(body[18..], listingPosition.Offset);
            BinaryPrimitives.WriteUInt32LittleEndian(body[20..], NoXattr);
            inodes.Write(inode);
        }

        return position;
    }

    private SquashFsMetadataRef WriteFile(SquashFsFile file)
    {
        var content = file.Content;
        var start = content.Length == 0 ? 0UL : (ulong)image.Position;
        var blockSizes = new List<uint>();

        for (var offset = 0; offset < content.Length; offset += BlockSize)
        {
            var raw = content.AsSpan(offset, Math.Min(BlockSize, content.Length - offset)).ToArray();
            var compressed = Compress(raw);
            if (compressed.Length < raw.Length)
            {
                image.Write(compressed);
                blockSizes.Add((uint)compressed.Length);
            }
            else
            {
                image.Write(raw);
                blockSizes.Add((uint)raw.Length | UncompressedDataBlockFlag);
            }
        }

        var position = inodes.Position;
        byte[] inode;
        int blockListOffset;

        if (start <= uint.MaxValue)
        {
            inode = InodeHeader(FileType, file, 16 + blockSizes.Count * sizeof(uint));
            var body = inode.AsSpan(16);
            BinaryPrimitives.WriteUInt32LittleEndian(body[0..], (uint)start);
            BinaryPrimitives.WriteUInt32LittleEndian(body[4..], NoFragment);
            BinaryPrimitives.WriteUInt32LittleEndian(body[8..], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(body[12..], (uint)content.Length);
            blockListOffset = 32;
        }
        else
        {
            inode = InodeHeader(ExtendedFileType, file, 40 + blockSizes.Count * sizeof(uint));
            var body = inode.AsSpan(16);
            BinaryPrimitives.WriteUInt64LittleEndian(body[0..], start);
            BinaryPrimitives.WriteUInt64LittleEndian(body[8..], (ulong)content.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(body[16..], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(body[24..], 1);
            BinaryPrimitives.WriteUInt32LittleEndian(body[28..], NoFragment);
            BinaryPrimitives.WriteUInt32LittleEndian(body[32..], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(body[36..], NoXattr);
            blockListOffset = 56;
        }

        for (var i = 0; i < blockSizes.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(inode.AsSpan(blockListOffset + i * sizeof(uint)), blockSizes[i]);
        }

        inodes.Write(inode);
        return position;
    }

    /// <summary>
    /// The mode holds permission bits only: the kernel derives the file type from the inode
    /// type and rejects inodes whose mode already carries one.
    /// </summary>
    private byte[] InodeHeader(ushort type, SquashFsNode node, int bodyLength)
    {
        var idIndex = IdIndex(node.OwnerId);
        var inode = new byte[16 + bodyLength];
        BinaryPrimitives.WriteUInt16LittleEndian(inode.AsSpan(0), type);
        BinaryPrimitives.WriteUInt16LittleEndian(inode.AsSpan(2), (ushort)(node.Mode & PermissionBits));
        BinaryPrimitives.WriteUInt16LittleEndian(inode.AsSpan(4), idIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(inode.AsSpan(6), idIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(inode.AsSpan(8), modificationTime);
        BinaryPrimitives.WriteUInt32LittleEndian(inode.AsSpan(12), inodeNumbers[node]);
        return inode;
    }

    /// <summary>
    /// Every entry stores its inode number as a signed 16-bit delta from the header, and all
    /// entries under one header must have their inodes in the same metadata block.
    /// </summary>
    private static byte[] BuildListing(IReadOnlyList<DirectoryEntry> entries)
    {
        using var listing = new MemoryStream();
        var index = 0;

        while (index < entries.Count)
        {
            var first = entries[index];
            var run = entries
                .Skip(index)
                .Take(MaxEntriesPerHeader)
                .TakeWhile(entry => entry.Inode.BlockStart == first.Inode.BlockStart && (long)entry.InodeNumber - first.InodeNumber is >= short.MinValue and <= short.MaxValue)
                .ToList();

            var header = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), (uint)run.Count - 1);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), first.Inode.BlockStart);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), first.InodeNumber);
            listing.Write(header);

            foreach (var entry in run)
            {
                var record = new byte[8 + entry.Name.Length];
                BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0), entry.Inode.Offset);
                BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(2), (short)((long)entry.InodeNumber - first.InodeNumber));
                BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), entry.Type);
                BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(6), (ushort)(entry.Name.Length - 1));
                entry.Name.CopyTo(record, 8);
                listing.Write(record);
            }

            index += run.Count;
        }

        return listing.ToArray();
    }

    private ulong WriteIdTable()
    {
        var blocksStart = (ulong)image.Position;
        var writer = new SquashFsMetadataWriter(Compress);
        var blockPointers = new List<ulong>();
        var buffer = new byte[sizeof(uint)];

        for (var i = 0; i < ids.Count; i++)
        {
            if (i % IdsPerMetadataBlock == 0)
            {
                blockPointers.Add(blocksStart + writer.Position.BlockStart);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, ids[i]);
            writer.Write(buffer);
        }

        image.Write(writer.ToArray());

        var tableStart = (ulong)image.Position;
        var pointer = new byte[sizeof(ulong)];
        foreach (var blockPointer in blockPointers)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(pointer, blockPointer);
            image.Write(pointer);
        }

        return tableStart;
    }

    private void WriteSuperblock(SquashFsMetadataRef rootInode, ulong bytesUsed, ulong idTableStart, ulong inodeTableStart, ulong directoryTableStart)
    {
        var superblock = new byte[SuperblockSize];
        var span = superblock.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)inodeNumbers.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], modificationTime);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], BlockSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], GzipCompressor);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], BlockLog);
        BinaryPrimitives.WriteUInt16LittleEndian(span[24..], NoFragmentsFlag | NoXattrsFlag);
        BinaryPrimitives.WriteUInt16LittleEndian(span[26..], (ushort)ids.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(span[28..], 4);
        BinaryPrimitives.WriteUInt16LittleEndian(span[30..], 0);
        BinaryPrimitives.WriteUInt64LittleEndian(span[32..], rootInode.Reference);
        BinaryPrimitives.WriteUInt64LittleEndian(span[40..], bytesUsed);
        BinaryPrimitives.WriteUInt64LittleEndian(span[48..], idTableStart);
        BinaryPrimitives.WriteUInt64LittleEndian(span[56..], NotPresent);
        BinaryPrimitives.WriteUInt64LittleEndian(span[64..], inodeTableStart);
        BinaryPrimitives.WriteUInt64LittleEndian(span[72..], directoryTableStart);
        BinaryPrimitives.WriteUInt64LittleEndian(span[80..], NotPresent);
        BinaryPrimitives.WriteUInt64LittleEndian(span[88..], NotPresent);

        image.Position = 0;
        image.Write(superblock);
        image.Position = image.Length;
    }

    private void PadToDeviceSize()
    {
        var remainder = (int)(image.Length % DevicePadding);
        if (remainder != 0)
        {
            image.Write(new byte[DevicePadding - remainder]);
        }
    }

    private ushort IdIndex(uint id)
    {
        var index = ids.IndexOf(id);
        if (index >= 0)
        {
            return (ushort)index;
        }

        if (ids.Count == ushort.MaxValue)
        {
            throw new InvalidOperationException("SquashFS images support at most 65535 distinct owner ids");
        }

        ids.Add(id);
        return (ushort)(ids.Count - 1);
    }

    private static IEnumerable<(SquashFsNode Node, byte[] Name)> SortedChildren(SquashFsDirectory directory)
    {
        var children = directory.Children
            .Select(child => (Node: child, Name: EncodeName(child.Name, directory.Name)))
            .OrderBy(child => child.Name, NameOrder)
            .ToList();

        var duplicate = children
            .Zip(children.Skip(1))
            .FirstOrDefault(pair => pair.First.Name.AsSpan().SequenceEqual(pair.Second.Name));
        if (duplicate.First.Node is not null)
        {
            throw new InvalidOperationException($"Duplicate entry '{duplicate.First.Node.Name}' in directory '{directory.Name}'");
        }

        return children;
    }

    private static byte[] EncodeName(string name, string directoryName)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        if (bytes.Length is 0 or > MaxNameLength || name is "." or ".." || name.Contains('/') || name.Contains('\0'))
        {
            throw new InvalidOperationException($"Invalid SquashFS entry name '{name}' in directory '{directoryName}'");
        }

        return bytes;
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private sealed record DirectoryEntry(byte[] Name, SquashFsMetadataRef Inode, uint InodeNumber, ushort Type);
}
