using System.Buffers.Binary;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Dmg.Verification;

internal static class HfsPlusVerifier
{
    private const int VolumeHeaderOffset = 1024;
    private const int VolumeHeaderSize = 512;
    private const ushort HfsPlusSignature = 0x482B;
    private const int FileCountOffset = 32;
    private const int BlockSizeOffset = 40;
    private const int CatalogFileOffset = 272;
    private const int ForkDataLogicalSizeOffset = 0;
    private const int ForkDataTotalBlocksOffset = 12;
    private const int ForkDataFirstExtentOffset = 16;
    private const int ExtentStartBlockOffset = 0;
    private const int ExtentBlockCountOffset = 4;
    private const int NodeDescriptorSize = 14;
    private const byte LeafNodeKind = 0xFF;
    private const ushort CatalogFileRecordType = 0x0002;

    public static Result<HfsPlusVerification> Verify(byte[] volume)
    {
        try
        {
            if (volume.Length < VolumeHeaderOffset + VolumeHeaderSize)
            {
                return Result.Failure<HfsPlusVerification>("HFS+ payload is too small");
            }

            var header = volume.AsSpan(VolumeHeaderOffset, VolumeHeaderSize);
            var signature = BinaryPrimitives.ReadUInt16BigEndian(header);
            if (signature != HfsPlusSignature)
            {
                return Result.Failure<HfsPlusVerification>("HFS+ signature not found");
            }

            var headerFileCount = BinaryPrimitives.ReadUInt32BigEndian(header[FileCountOffset..]);
            var blockSize = BinaryPrimitives.ReadUInt32BigEndian(header[BlockSizeOffset..]);
            if (blockSize == 0)
            {
                return Result.Failure<HfsPlusVerification>("HFS+ block size is zero");
            }

            if (blockSize > int.MaxValue)
            {
                return Result.Failure<HfsPlusVerification>("HFS+ block size is too large");
            }

            var catalogFork = header[CatalogFileOffset..];
            var catalogLogicalSize = BinaryPrimitives.ReadUInt64BigEndian(catalogFork[ForkDataLogicalSizeOffset..]);
            var catalogTotalBlocks = BinaryPrimitives.ReadUInt32BigEndian(catalogFork[ForkDataTotalBlocksOffset..]);
            var firstExtent = catalogFork[ForkDataFirstExtentOffset..];
            var catalogStartBlock = BinaryPrimitives.ReadUInt32BigEndian(firstExtent[ExtentStartBlockOffset..]);
            var catalogExtentBlocks = BinaryPrimitives.ReadUInt32BigEndian(firstExtent[ExtentBlockCountOffset..]);

            if (catalogLogicalSize == 0 || catalogTotalBlocks == 0 || catalogExtentBlocks == 0)
            {
                return Result.Failure<HfsPlusVerification>("HFS+ catalog file is empty");
            }

            var catalogOffset = checked((long)catalogStartBlock * blockSize);
            var catalogExtentLength = checked((long)catalogExtentBlocks * blockSize);
            if (catalogOffset < 0 || catalogOffset >= volume.Length || catalogOffset + catalogExtentLength > volume.Length)
            {
                return Result.Failure<HfsPlusVerification>("HFS+ catalog extent is outside the payload");
            }

            var catalogLength = (int)Math.Min(catalogLogicalSize, (ulong)catalogExtentLength);
            var catalog = volume.AsSpan((int)catalogOffset, catalogLength);
            var catalogFileRecords = CountCatalogFileRecords(catalog, (int)blockSize);
            if (catalogFileRecords.IsFailure)
            {
                return Result.Failure<HfsPlusVerification>(catalogFileRecords.Error);
            }

            if (headerFileCount != catalogFileRecords.Value)
            {
                return Result.Failure<HfsPlusVerification>($"HFS+ file count mismatch: header={headerFileCount}, catalog={catalogFileRecords.Value}");
            }

            return Result.Success(new HfsPlusVerification(headerFileCount));
        }
        catch (OverflowException)
        {
            return Result.Failure<HfsPlusVerification>("HFS+ catalog offsets overflow");
        }
    }

    private static Result<uint> CountCatalogFileRecords(ReadOnlySpan<byte> catalog, int headerNodeSize)
    {
        var headerRecord = ReadRecord(catalog, nodeOffset: 0, nodeSize: headerNodeSize, recordIndex: 0);
        if (headerRecord.IsFailure)
        {
            return Result.Failure<uint>($"HFS+ catalog header is invalid: {headerRecord.Error}");
        }

        var bTreeHeader = headerRecord.Value;
        var bTreeHeaderSpan = bTreeHeader.Span;
        if (bTreeHeaderSpan.Length < 26)
        {
            return Result.Failure<uint>("HFS+ catalog B-tree header is too small");
        }

        var firstLeafNode = BinaryPrimitives.ReadUInt32BigEndian(bTreeHeaderSpan[10..]);
        var nodeSize = BinaryPrimitives.ReadUInt16BigEndian(bTreeHeaderSpan[18..]);
        var totalNodes = BinaryPrimitives.ReadUInt32BigEndian(bTreeHeaderSpan[22..]);
        if (nodeSize == 0)
        {
            return Result.Failure<uint>("HFS+ catalog B-tree node size is zero");
        }

        if (firstLeafNode == 0)
        {
            return Result.Success(0u);
        }

        uint fileRecords = 0;
        var currentNode = firstLeafNode;
        var visited = new HashSet<uint>();

        while (currentNode != 0)
        {
            if (!visited.Add(currentNode))
            {
                return Result.Failure<uint>("HFS+ catalog leaf chain contains a cycle");
            }

            if (totalNodes != 0 && currentNode >= totalNodes)
            {
                return Result.Failure<uint>($"HFS+ catalog leaf node {currentNode} is outside the B-tree");
            }

            var nodeOffset = checked((long)currentNode * nodeSize);
            if (nodeOffset < 0 || nodeOffset + nodeSize > catalog.Length)
            {
                return Result.Failure<uint>($"HFS+ catalog leaf node {currentNode} is outside the catalog file");
            }

            var node = catalog.Slice((int)nodeOffset, nodeSize);
            if (node[8] != LeafNodeKind)
            {
                return Result.Failure<uint>($"HFS+ catalog node {currentNode} is not a leaf node");
            }

            var recordCount = BinaryPrimitives.ReadUInt16BigEndian(node[10..]);
            for (var i = 0; i < recordCount; i++)
            {
                var record = ReadRecord(catalog, nodeOffset, nodeSize, i);
                if (record.IsFailure)
                {
                    return Result.Failure<uint>($"HFS+ catalog leaf record {i} in node {currentNode} is invalid: {record.Error}");
                }

                if (IsCatalogFileRecord(record.Value))
                {
                    fileRecords++;
                }
            }

            currentNode = BinaryPrimitives.ReadUInt32BigEndian(node);
        }

        return Result.Success(fileRecords);
    }

    private static Result<ReadOnlyMemory<byte>> ReadRecord(ReadOnlySpan<byte> tree, long nodeOffset, int nodeSize, int recordIndex)
    {
        if (nodeSize < NodeDescriptorSize || nodeOffset < 0 || nodeOffset + nodeSize > tree.Length)
        {
            return Result.Failure<ReadOnlyMemory<byte>>("node bounds are invalid");
        }

        var node = tree.Slice((int)nodeOffset, nodeSize);
        var recordCount = BinaryPrimitives.ReadUInt16BigEndian(node[10..]);
        if (recordIndex < 0 || recordIndex >= recordCount)
        {
            return Result.Failure<ReadOnlyMemory<byte>>("record index is outside the node");
        }

        var startOffsetIndex = nodeSize - ((recordIndex + 1) * 2);
        var endOffsetIndex = nodeSize - ((recordIndex + 2) * 2);
        if (endOffsetIndex < 0)
        {
            return Result.Failure<ReadOnlyMemory<byte>>("record offset table is outside the node");
        }

        var recordStart = BinaryPrimitives.ReadUInt16BigEndian(node[startOffsetIndex..]);
        var recordEnd = BinaryPrimitives.ReadUInt16BigEndian(node[endOffsetIndex..]);
        if (recordStart < NodeDescriptorSize || recordEnd < recordStart || recordEnd > nodeSize)
        {
            return Result.Failure<ReadOnlyMemory<byte>>("record bounds are invalid");
        }

        ReadOnlyMemory<byte> record = tree.Slice((int)nodeOffset + recordStart, recordEnd - recordStart).ToArray();
        return Result.Success(record);
    }

    private static bool IsCatalogFileRecord(ReadOnlyMemory<byte> recordMemory)
    {
        var record = recordMemory.Span;
        if (record.Length < 4)
        {
            return false;
        }

        var keyLength = BinaryPrimitives.ReadUInt16BigEndian(record);
        var recordTypeOffset = 2 + keyLength;
        if (recordTypeOffset + 2 > record.Length)
        {
            return false;
        }

        return BinaryPrimitives.ReadUInt16BigEndian(record[recordTypeOffset..]) == CatalogFileRecordType;
    }
}

internal sealed record HfsPlusVerification(uint FileCount);
