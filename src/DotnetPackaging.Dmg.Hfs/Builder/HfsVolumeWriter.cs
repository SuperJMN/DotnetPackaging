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
        
        // 1. Calculate Reserved Blocks (Headers & Wrappers)
        // Block 0: Boot Blocks (0-1024) + Volume Header (1024-1536).
        // If BlockSize=4096, this is just Block 0.
        // If BlockSize=512, this is Blocks 0, 1, 2.
        var headerBytes = BootBlocksSize + VolumeHeader.Size; // 1536 bytes
        var headerBlocks = (uint)((headerBytes + blockSize - 1) / blockSize);

        // Last Block: Alt Volume Header (last 1024 bytes area)
        var altHeaderBytes = SectorSize * 2; // Reserved + AltHeader = 1024 bytes
        var altHeaderBlocks = (uint)((altHeaderBytes + blockSize - 1) / blockSize);

        // 2. Estimate Metadata Sizes
        var nextCnid = CatalogNodeId.FirstUserCatalogNodeId.Value;
        // Build catalog initially to get size
        var catalog = BuildCatalog(volume, fileDataList, symlinkDataList, out _);
        var extents = new ExtentsBTree((int)blockSize);
        var attributes = new AttributesBTree((int)blockSize);
        
        var catalogBlocks = (uint)((catalog.TotalSize + blockSize - 1) / blockSize);
        // Force allocation of Extents and Attributes to comply with standard macOS layout
        var extentsBlocks = 1u; // Minimum 1 block
        var attributesBlocks = 1u; // Minimum 1 block
        
        // Calculate data blocks
        var dataBlocksNeeded = (uint)fileDataList.Sum(f => (f.data.Length + (long)blockSize - 1) / blockSize)
                             + (uint)symlinkDataList.Sum(s => (s.data.Length + (long)blockSize - 1) / blockSize);

        // 3. Iterate to find TotalBlocks (converge on AllocationFile size)
        var fixedBlocks = headerBlocks + altHeaderBlocks + catalogBlocks + extentsBlocks + attributesBlocks + dataBlocksNeeded;
        
        uint allocationBlocks = 1;
        uint totalBlocks;
        int allocationBytes;
        
        while (true)
        {
            var previousAllocationBlocks = allocationBlocks;
            totalBlocks = fixedBlocks + allocationBlocks;
            allocationBytes = (int)((totalBlocks + 7) / 8);
            allocationBlocks = (uint)((allocationBytes + blockSize - 1) / blockSize);
            
            if (allocationBlocks <= previousAllocationBlocks)
            {
                allocationBlocks = previousAllocationBlocks; // Stabilized
                totalBlocks = fixedBlocks + allocationBlocks;
                break;
            }
        }

        // 4. Build Bitmap and Assign Blocks
        var allocation = new AllocationBitmap(totalBlocks);
        
        // Reserve Header Blocks
        allocation.MarkUsed(0, headerBlocks);
        // Reserve Alt Header Blocks (at end)
        // Careful: The last blocks are indices [TotalBlocks - altHeaderBlocks ... TotalBlocks - 1]
        allocation.MarkUsed(totalBlocks - altHeaderBlocks, altHeaderBlocks);

        // Current allocator pointer starts after headers
        var allocatorSearchStart = headerBlocks; // Point where to start searching for free blocks
        var nextBlock = headerBlocks;

        // Use a helper to allocate contiguous blocks
        uint Allocate(uint count)
        {
            if (count == 0) return 0;
            var start = allocation.Allocate(count, nextBlock); 
            if (start == null)
                 throw new InvalidOperationException($"Not enough contiguous space for {count} blocks");
            
            nextBlock = start.Value + count;
            return start.Value;
        }

        // Allocate Metadata
        // Standard HFS+ Layout preference: Wrapper -> Allocation -> Extents -> Catalog -> Attributes
        var allocationStartBlock = Allocate(allocationBlocks);
        var extentsStartBlock = Allocate(extentsBlocks);
        var catalogStartBlock = Allocate(catalogBlocks);
        var attributesStartBlock = Allocate(attributesBlocks);

        // Allocate Files
        for (var i = 0; i < fileDataList.Count; i++)
        {
            var (file, data, _, _) = fileDataList[i];
            var blocks = (uint)((data.Length + blockSize - 1) / blockSize);
            fileDataList[i] = (file, data, Allocate(blocks), blocks);
        }

        for (var i = 0; i < symlinkDataList.Count; i++)
        {
            var (symlink, data, _, _) = symlinkDataList[i];
            var blocks = (uint)((data.Length + blockSize - 1) / blockSize);
            symlinkDataList[i] = (symlink, data, Allocate(blocks), blocks);
        }

        // 5. Rebuild Catalog with finalized block numbers
        catalog = BuildCatalogWithBlocks(volume, fileDataList, symlinkDataList, out nextCnid);
        
        // 6. Create Volume Header
        var now = DateTime.UtcNow;
        var header = new VolumeHeader
        {
            CreateDate = now,
            ModifyDate = now,
            CheckedDate = now,
            FileCount = fileCount + (uint)symlinkDataList.Count,
            FolderCount = folderCount, // Remove +1 as fsck counts 13 matching folderCount
            BlockSize = blockSize,
            TotalBlocks = totalBlocks,
            FreeBlocks = allocation.FreeBlocks,
            NextCatalogId = nextCnid,
            NextAllocation = allocatorSearchStart, // Reset hint to start of free usage for OS
            EncodingsBitmap = 1, // MacRoman (Bit 0)
            AllocationFile = ForkData.FromExtent((ulong)allocationBytes, allocationStartBlock, allocationBlocks),
            ExtentsFile = ForkData.FromExtent(blockSize, extentsStartBlock, extentsBlocks),
            CatalogFile = ForkData.FromExtent((ulong)catalog.TotalSize, catalogStartBlock, catalogBlocks),
            AttributesFile = ForkData.FromExtent(blockSize, attributesStartBlock, attributesBlocks) with { ClumpSize = blockSize }
        };

        // 7. Write Result Buffer
        // Total size is exactly TotalBlocks * BlockSize
        var totalSizeBytes = (long)totalBlocks * blockSize;
        var buffer = new byte[totalSizeBytes];

        // Helper to write data at block offset
        void WriteAtBlock(uint blockIdx, byte[] data)
        {
            if (blockIdx == 0 && data.Length == 0) return;
             // Determine exact byte offset
            var offset = (long)blockIdx * blockSize;
            data.CopyTo(buffer.AsSpan((int)offset)); // Assuming DMG < 2GB for array
        }
        
        // Write Headers
        // Boot blocks are 0-1024 (Zeroed)
        // Volume header is 1024-1536
        header.WriteTo(buffer.AsSpan(VolumeHeaderOffset, VolumeHeader.Size));

        // Write Metadata
        WriteAtBlock(allocationStartBlock, allocation.ToBytes());
        WriteAtBlock(extentsStartBlock, extents.ToBytes()); // if size > 0
        WriteAtBlock(catalogStartBlock, catalog.ToBytes());
        WriteAtBlock(attributesStartBlock, attributes.ToBytes());
        
        // Write Files
        foreach (var (_, data, start, _) in fileDataList) WriteAtBlock(start, data);
        foreach (var (_, data, start, _) in symlinkDataList) WriteAtBlock(start, data);

        // Write Alt Header
        // Located in second-to-last 512-byte sector.
        // Offset = TotalSizeBytes - 1024
        var altHeaderOffset = (int)(totalSizeBytes - 1024);
        header.WriteTo(buffer.AsSpan(altHeaderOffset, VolumeHeader.Size));

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
        catalog.AddRootFolder(volume.VolumeName, rootValence);

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
        catalog.AddRootFolder(volume.VolumeName, rootValence);

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
