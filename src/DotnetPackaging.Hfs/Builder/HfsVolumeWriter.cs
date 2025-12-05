using DotnetPackaging.Hfs.Attributes;
using DotnetPackaging.Hfs.Catalog;
using DotnetPackaging.Hfs.Extents;
using DotnetPackaging.Hfs.Files;

namespace DotnetPackaging.Hfs.Builder;

/// <summary>
/// Writes an HFS+ volume to bytes.
/// Layout:
/// - Boot blocks (sectors 0-1): 1024 bytes (zeroed)
/// - Volume header (sector 2): 512 bytes
/// - Allocation file
/// - Extents overflow file
/// - Catalog file
/// - Attributes file
/// - File data blocks
/// - Alternate volume header (second-to-last sector)
/// - Reserved (last sector): 512 bytes
/// </summary>
public static class HfsVolumeWriter
{
    private const int SectorSize = 512;
    private const int BootBlocksSize = 1024; // Sectors 0-1
    private const int VolumeHeaderOffset = 1024; // Sector 2

    /// <summary>
    /// Writes an HFS+ volume and returns an IByteSource.
    /// </summary>
    public static IByteSource Write(HfsVolume volume)
    {
        var bytes = WriteToBytes(volume);
        return ByteSource.FromBytes(bytes);
    }

    /// <summary>
    /// Writes an HFS+ volume to a byte array.
    /// </summary>
    public static byte[] WriteToBytes(HfsVolume volume)
    {
        var blockSize = volume.BlockSize;
        var (fileCount, folderCount) = volume.CountEntries();

        // Collect all files and their data
        var fileDataList = new List<(HfsFile file, byte[] data, uint startBlock, uint blockCount)>();
        var symlinkDataList = new List<(HfsSymlink symlink, byte[] data, uint startBlock, uint blockCount)>();
        
        CollectFileData(volume.Root, fileDataList, symlinkDataList);

        // Calculate sizes
        var catalog = BuildCatalog(volume, fileDataList, symlinkDataList, out var nextCnid);
        var extents = new ExtentsBTree((int)blockSize);
        var attributes = new AttributesBTree((int)blockSize);

        // Calculate total data size
        long totalDataSize = fileDataList.Sum(f => f.data.Length) + symlinkDataList.Sum(s => s.data.Length);
        var dataBlocksNeeded = (uint)((totalDataSize + blockSize - 1) / blockSize);

        // Calculate special file sizes in blocks
        var catalogBlocks = (uint)((catalog.TotalSize + blockSize - 1) / blockSize);
        var extentsBlocks = (uint)((extents.TotalSize + blockSize - 1) / blockSize);
        var attributesBlocks = (uint)((attributes.TotalSize + blockSize - 1) / blockSize);

        // Allocation bitmap size (1 bit per block)
        // We need to know total blocks first, but that depends on allocation size
        // Start with estimate and iterate if needed
        var estimatedTotalBlocks = 64u + catalogBlocks + extentsBlocks + attributesBlocks + dataBlocksNeeded;
        var allocationBytes = (int)((estimatedTotalBlocks + 7) / 8);
        var allocationBlocks = (uint)((allocationBytes + blockSize - 1) / blockSize);

        // Final total (add some padding)
        var totalBlocks = 3u + allocationBlocks + extentsBlocks + catalogBlocks + attributesBlocks + dataBlocksNeeded + 2;
        
        // Recalculate allocation with actual total
        allocationBytes = (int)((totalBlocks + 7) / 8);
        allocationBlocks = (uint)((allocationBytes + blockSize - 1) / blockSize);

        // Build allocation bitmap
        var allocation = new AllocationBitmap(totalBlocks);

        // Reserve boot blocks and volume header (blocks 0-2 conceptually)
        // In HFS+, the first allocation block starts after the volume header
        var nextBlock = 0u;

        // Allocate extents file (typically at the beginning)
        var extentsStartBlock = nextBlock;
        allocation.MarkUsed(extentsStartBlock, extentsBlocks);
        nextBlock += extentsBlocks;

        // Allocate catalog file
        var catalogStartBlock = nextBlock;
        allocation.MarkUsed(catalogStartBlock, catalogBlocks);
        nextBlock += catalogBlocks;

        // Allocate attributes file
        var attributesStartBlock = nextBlock;
        allocation.MarkUsed(attributesStartBlock, attributesBlocks);
        nextBlock += attributesBlocks;

        // Allocate allocation file itself
        var allocationStartBlock = nextBlock;
        allocation.MarkUsed(allocationStartBlock, allocationBlocks);
        nextBlock += allocationBlocks;

        // Allocate file data
        for (var i = 0; i < fileDataList.Count; i++)
        {
            var (file, data, _, _) = fileDataList[i];
            var fileBlocks = (uint)((data.Length + blockSize - 1) / blockSize);
            fileDataList[i] = (file, data, nextBlock, fileBlocks);
            allocation.MarkUsed(nextBlock, fileBlocks);
            nextBlock += fileBlocks;
        }

        for (var i = 0; i < symlinkDataList.Count; i++)
        {
            var (symlink, data, _, _) = symlinkDataList[i];
            var linkBlocks = (uint)((data.Length + blockSize - 1) / blockSize);
            symlinkDataList[i] = (symlink, data, nextBlock, linkBlocks);
            allocation.MarkUsed(nextBlock, linkBlocks);
            nextBlock += linkBlocks;
        }

        // Rebuild catalog with correct block assignments
        catalog = BuildCatalogWithBlocks(volume, fileDataList, symlinkDataList, out nextCnid);

        // Create volume header
        var now = DateTime.UtcNow;
        var header = new VolumeHeader
        {
            CreateDate = now,
            ModifyDate = now,
            CheckedDate = now,
            FileCount = fileCount + (uint)symlinkDataList.Count,
            FolderCount = folderCount + 1, // +1 for root
            BlockSize = blockSize,
            TotalBlocks = totalBlocks,
            FreeBlocks = allocation.FreeBlocks,
            NextCatalogId = nextCnid,
            AllocationFile = ForkData.FromExtent((ulong)allocationBytes, allocationStartBlock, allocationBlocks),
            ExtentsFile = ForkData.FromExtent((ulong)extents.TotalSize, extentsStartBlock, extentsBlocks),
            CatalogFile = ForkData.FromExtent((ulong)catalog.TotalSize, catalogStartBlock, catalogBlocks),
            AttributesFile = ForkData.FromExtent((ulong)attributes.TotalSize, attributesStartBlock, attributesBlocks)
        };

        // Calculate total volume size
        var totalSize = (long)totalBlocks * blockSize + BootBlocksSize + VolumeHeader.Size * 2 + SectorSize;
        // Round up to block boundary
        totalSize = ((totalSize + blockSize - 1) / blockSize) * blockSize;

        // Write everything
        var buffer = new byte[totalSize];

        // Boot blocks (zeroed) - already zero

        // Volume header at offset 1024
        header.WriteTo(buffer.AsSpan(VolumeHeaderOffset, VolumeHeader.Size));

        // Calculate where allocation blocks start
        var allocBlockStart = BootBlocksSize + VolumeHeader.Size;
        // Align to block boundary
        allocBlockStart = (int)(((allocBlockStart + blockSize - 1) / blockSize) * blockSize);

        // Write extents file
        var extentsOffset = allocBlockStart + (int)(extentsStartBlock * blockSize);
        extents.ToBytes().CopyTo(buffer.AsSpan(extentsOffset));

        // Write catalog file
        var catalogOffset = allocBlockStart + (int)(catalogStartBlock * blockSize);
        catalog.ToBytes().CopyTo(buffer.AsSpan(catalogOffset));

        // Write attributes file
        var attributesOffset = allocBlockStart + (int)(attributesStartBlock * blockSize);
        attributes.ToBytes().CopyTo(buffer.AsSpan(attributesOffset));

        // Write allocation file
        var allocationOffset = allocBlockStart + (int)(allocationStartBlock * blockSize);
        allocation.ToBytes().CopyTo(buffer.AsSpan(allocationOffset));

        // Write file data
        foreach (var (_, data, startBlock, _) in fileDataList)
        {
            var offset = allocBlockStart + (int)(startBlock * blockSize);
            data.CopyTo(buffer.AsSpan(offset));
        }

        foreach (var (_, data, startBlock, _) in symlinkDataList)
        {
            var offset = allocBlockStart + (int)(startBlock * blockSize);
            data.CopyTo(buffer.AsSpan(offset));
        }

        // Alternate volume header (second-to-last sector)
        var altHeaderOffset = buffer.Length - SectorSize * 2;
        header.WriteTo(buffer.AsSpan(altHeaderOffset, VolumeHeader.Size));

        // Last sector reserved (already zeroed)

        return buffer;
    }

    private static void CollectFileData(
        HfsDirectory dir,
        List<(HfsFile file, byte[] data, uint startBlock, uint blockCount)> fileDataList,
        List<(HfsSymlink symlink, byte[] data, uint startBlock, uint blockCount)> symlinkDataList)
    {
        foreach (var entry in dir.Children)
        {
            switch (entry)
            {
                case HfsFile file:
                    var fileData = file.Content.ToStream().ReadAllBytes();
                    fileDataList.Add((file, fileData, 0, 0));
                    break;
                case HfsSymlink symlink:
                    var linkData = System.Text.Encoding.UTF8.GetBytes(symlink.Target);
                    symlinkDataList.Add((symlink, linkData, 0, 0));
                    break;
                case HfsDirectory subDir:
                    CollectFileData(subDir, fileDataList, symlinkDataList);
                    break;
            }
        }
    }

    private static CatalogBTree BuildCatalog(
        HfsVolume volume,
        List<(HfsFile file, byte[] data, uint startBlock, uint blockCount)> fileDataList,
        List<(HfsSymlink symlink, byte[] data, uint startBlock, uint blockCount)> symlinkDataList,
        out uint nextCnid)
    {
        var catalog = new CatalogBTree((int)volume.BlockSize);
        nextCnid = CatalogNodeId.FirstUserCatalogNodeId.Value;

        // Add root folder
        var rootValence = (uint)volume.Root.Children.Count;
        catalog.AddRootFolder(rootValence);

        // Add entries recursively
        AddEntriesRecursive(catalog, CatalogNodeId.RootFolder, volume.Root, fileDataList, symlinkDataList, ref nextCnid, volume.BlockSize);

        return catalog;
    }

    private static CatalogBTree BuildCatalogWithBlocks(
        HfsVolume volume,
        List<(HfsFile file, byte[] data, uint startBlock, uint blockCount)> fileDataList,
        List<(HfsSymlink symlink, byte[] data, uint startBlock, uint blockCount)> symlinkDataList,
        out uint nextCnid)
    {
        var catalog = new CatalogBTree((int)volume.BlockSize);
        nextCnid = CatalogNodeId.FirstUserCatalogNodeId.Value;

        // Add root folder
        var rootValence = (uint)volume.Root.Children.Count;
        catalog.AddRootFolder(rootValence);

        // Add entries recursively with block info
        AddEntriesWithBlocksRecursive(catalog, CatalogNodeId.RootFolder, volume.Root, fileDataList, symlinkDataList, ref nextCnid, volume.BlockSize);

        return catalog;
    }

    private static void AddEntriesRecursive(
        CatalogBTree catalog,
        CatalogNodeId parentId,
        HfsDirectory dir,
        List<(HfsFile file, byte[] data, uint startBlock, uint blockCount)> fileDataList,
        List<(HfsSymlink symlink, byte[] data, uint startBlock, uint blockCount)> symlinkDataList,
        ref uint nextCnid,
        uint blockSize)
    {
        foreach (var entry in dir.Children)
        {
            var cnid = new CatalogNodeId(nextCnid++);

            switch (entry)
            {
                case HfsDirectory subDir:
                    var folder = new CatalogFolderRecord
                    {
                        FolderId = cnid,
                        Valence = (uint)subDir.Children.Count,
                        CreateDate = entry.CreateDate,
                        ContentModDate = entry.ModifyDate
                    };
                    catalog.AddFolder(parentId, entry.Name, folder);
                    AddEntriesRecursive(catalog, cnid, subDir, fileDataList, symlinkDataList, ref nextCnid, blockSize);
                    break;

                case HfsFile file:
                    var fileRecord = new CatalogFileRecord
                    {
                        FileId = cnid,
                        CreateDate = entry.CreateDate,
                        ContentModDate = entry.ModifyDate,
                        DataFork = new ForkData { LogicalSize = (ulong)file.Size }
                    };
                    catalog.AddFile(parentId, entry.Name, fileRecord);
                    break;

                case HfsSymlink symlink:
                    var symlinkRecord = CatalogFileRecord.ForSymlink(cnid, symlink.Target);
                    catalog.AddFile(parentId, entry.Name, symlinkRecord);
                    break;
            }
        }
    }

    private static void AddEntriesWithBlocksRecursive(
        CatalogBTree catalog,
        CatalogNodeId parentId,
        HfsDirectory dir,
        List<(HfsFile file, byte[] data, uint startBlock, uint blockCount)> fileDataList,
        List<(HfsSymlink symlink, byte[] data, uint startBlock, uint blockCount)> symlinkDataList,
        ref uint nextCnid,
        uint blockSize)
    {
        foreach (var entry in dir.Children)
        {
            var cnid = new CatalogNodeId(nextCnid++);

            switch (entry)
            {
                case HfsDirectory subDir:
                    var folder = new CatalogFolderRecord
                    {
                        FolderId = cnid,
                        Valence = (uint)subDir.Children.Count,
                        CreateDate = entry.CreateDate,
                        ContentModDate = entry.ModifyDate
                    };
                    catalog.AddFolder(parentId, entry.Name, folder);
                    AddEntriesWithBlocksRecursive(catalog, cnid, subDir, fileDataList, symlinkDataList, ref nextCnid, blockSize);
                    break;

                case HfsFile file:
                    var fileInfo = fileDataList.FirstOrDefault(f => f.file == file);
                    var fileRecord = new CatalogFileRecord
                    {
                        FileId = cnid,
                        CreateDate = entry.CreateDate,
                        ContentModDate = entry.ModifyDate,
                        DataFork = ForkData.FromExtent((ulong)file.Size, fileInfo.startBlock, fileInfo.blockCount)
                    };
                    catalog.AddFile(parentId, entry.Name, fileRecord);
                    break;

                case HfsSymlink symlink:
                    var linkInfo = symlinkDataList.FirstOrDefault(s => s.symlink == symlink);
                    var symlinkRecord = new CatalogFileRecord
                    {
                        FileId = cnid,
                        Permissions = BsdInfo.ForSymlink(),
                        DataFork = ForkData.FromExtent((ulong)linkInfo.data.Length, linkInfo.startBlock, linkInfo.blockCount),
                        FinderInfo = new FileFinderInfo
                        {
                            FileType = 0x736C6E6B, // 'slnk'
                            FileCreator = 0x72686170 // 'rhap'
                        }
                    };
                    catalog.AddFile(parentId, entry.Name, symlinkRecord);
                    break;
            }
        }
    }
}

/// <summary>
/// Extension methods for Stream.
/// </summary>
internal static class StreamExtensions
{
    public static byte[] ReadAllBytes(this Stream stream)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
