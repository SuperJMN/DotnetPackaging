using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using DotnetPackaging.Msix.Core.Manifest;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using System.IO.Abstractions;

namespace MsixPackaging.Tests;

public class BlockMapConsistencyTests
{
    private const int BlockSize = 65536;

    public BlockMapConsistencyTests(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    /// <summary>
    /// MSIX stores each 64KB block as an independent deflate stream (for random access).
    /// ZipArchive.Open() only decompresses the first block because each has its own BFINAL bit.
    /// This test decompresses block-by-block using the block map sizes to verify hashes.
    /// </summary>
    [Fact]
    public async Task Block_hashes_match_actual_uncompressed_data()
    {
        var (packageBytes, blockMapXml) = await BuildPackageAndReadBlockMap("ValidExe");
        var doc = XDocument.Parse(blockMapXml);
        XNamespace bns = "http://schemas.microsoft.com/appx/2010/blockmap";

        foreach (var fileElement in doc.Root!.Elements(bns + "File"))
        {
            var fileName = fileElement.Attribute("Name")!.Value.Replace("\\", "/");
            var declaredSize = long.Parse(fileElement.Attribute("Size")!.Value);
            var blocks = fileElement.Elements(bns + "Block").ToList();
            if (blocks.Count == 0) continue;

            bool isCompressed = blocks.Any(b => b.Attribute("Size") != null);

            byte[] uncompressed;
            if (isCompressed)
            {
                uncompressed = DecompressBlockByBlock(packageBytes, fileName, blocks, bns);
            }
            else
            {
                var rawData = ReadRawCompressedData(packageBytes, fileName);
                uncompressed = rawData;
            }

            Assert.True(declaredSize == uncompressed.Length,
                $"Total size mismatch for {fileName}: block map says {declaredSize}, actual {uncompressed.Length}");

            int offset = 0;
            foreach (var blockElement in blocks)
            {
                var expectedHash = blockElement.Attribute("Hash")!.Value;
                var thisBlockSize = Math.Min(BlockSize, uncompressed.Length - offset);
                var blockData = uncompressed.AsSpan(offset, thisBlockSize).ToArray();
                var actualHash = Convert.ToBase64String(SHA256.HashData(blockData));

                Assert.True(expectedHash == actualHash,
                    $"Block hash mismatch in {fileName} at offset {offset}: " +
                    $"expected {expectedHash}, got {actualHash}");

                offset += thisBlockSize;
            }

            Assert.Equal(uncompressed.Length, offset);
        }
    }

    private static byte[] DecompressBlockByBlock(byte[] packageBytes, string fileName, List<XElement> blocks, XNamespace bns)
    {
        var rawCompressed = ReadRawCompressedData(packageBytes, fileName);
        using var result = new MemoryStream();

        long rawOffset = 0;
        foreach (var blockElement in blocks)
        {
            var sizeAttr = blockElement.Attribute("Size");
            if (sizeAttr == null) continue;

            var compressedBlockSize = int.Parse(sizeAttr.Value);
            var compressedBlock = rawCompressed.AsSpan((int)rawOffset, compressedBlockSize).ToArray();

            using var compMs = new MemoryStream(compressedBlock);
            using var deflate = new DeflateStream(compMs, CompressionMode.Decompress);
            deflate.CopyTo(result);

            rawOffset += compressedBlockSize;
        }

        return result.ToArray();
    }

    [Fact]
    public async Task Block_compressed_sizes_sum_equals_zip_entry_compressed_length()
    {
        var (packageBytes, blockMapXml) = await BuildPackageAndReadBlockMap("ValidExe");
        var doc = XDocument.Parse(blockMapXml);
        XNamespace bns = "http://schemas.microsoft.com/appx/2010/blockmap";

        using var ms = new MemoryStream(packageBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var fileElement in doc.Root!.Elements(bns + "File"))
        {
            var fileName = fileElement.Attribute("Name")!.Value.Replace("\\", "/");
            var blocks = fileElement.Elements(bns + "Block").ToList();
            if (blocks.Count == 0) continue;

            bool isCompressed = blocks.Any(b => b.Attribute("Size") != null);
            if (!isCompressed) continue;

            long blockMapTotal = blocks
                .Where(b => b.Attribute("Size") != null)
                .Sum(b => long.Parse(b.Attribute("Size")!.Value));

            var zipEntry = zip.GetEntry(fileName);
            Assert.NotNull(zipEntry);

            Assert.True(blockMapTotal == zipEntry.CompressedLength,
                $"Compressed size mismatch for {fileName}: " +
                $"block map says {blockMapTotal}, ZIP says {zipEntry.CompressedLength}");
        }
    }

    [Fact]
    public async Task Each_compressed_block_is_independently_decompressible()
    {
        var (packageBytes, blockMapXml) = await BuildPackageAndReadBlockMap("ValidExe");
        var doc = XDocument.Parse(blockMapXml);
        XNamespace bns = "http://schemas.microsoft.com/appx/2010/blockmap";

        foreach (var fileElement in doc.Root!.Elements(bns + "File"))
        {
            var fileName = fileElement.Attribute("Name")!.Value.Replace("\\", "/");
            var blocks = fileElement.Elements(bns + "Block").ToList();
            if (blocks.Count == 0) continue;

            bool isCompressed = blocks.Any(b => b.Attribute("Size") != null);
            if (!isCompressed) continue;

            var rawCompressed = ReadRawCompressedData(packageBytes, fileName);

            long rawOffset = 0;
            foreach (var blockElement in blocks)
            {
                var sizeAttr = blockElement.Attribute("Size");
                if (sizeAttr == null) continue;

                var compressedBlockSize = int.Parse(sizeAttr.Value);
                var expectedHash = blockElement.Attribute("Hash")!.Value;

                var compressedBlock = rawCompressed.AsSpan((int)rawOffset, compressedBlockSize).ToArray();

                using var compMs = new MemoryStream(compressedBlock);
                using var deflate = new DeflateStream(compMs, CompressionMode.Decompress);
                using var decompMs = new MemoryStream();
                await deflate.CopyToAsync(decompMs);
                var decompressed = decompMs.ToArray();

                var actualHash = Convert.ToBase64String(SHA256.HashData(decompressed));
                Assert.True(expectedHash == actualHash,
                    $"Independent decompression hash mismatch in {fileName} at raw offset {rawOffset}: " +
                    $"expected {expectedHash}, got {actualHash}");

                rawOffset += compressedBlockSize;
            }
        }
    }

    private static async Task<(byte[] Package, string BlockMapXml)> BuildPackageAndReadBlockMap(string folderName)
    {
        var fs = new FileSystem();
        var dirInfo = fs.DirectoryInfo.New($"TestFiles/{folderName}/Contents");
        var container = new DirectoryContainer(dirInfo);

        var packager = new MsixPackager();
        var result = await packager.Pack(container, Maybe<AppManifestMetadata>.None, logger: Log.Logger);
        Assert.True(result.IsSuccess);

        using var ms = new MemoryStream();
        await result.Value.WriteTo(ms);
        var packageBytes = ms.ToArray();

        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var blockMapEntry = zip.GetEntry("AppxBlockMap.xml");
        Assert.NotNull(blockMapEntry);

        using var reader = new StreamReader(blockMapEntry.Open(), Encoding.UTF8);
        var blockMapXml = await reader.ReadToEndAsync();

        return (packageBytes, blockMapXml);
    }

    private static byte[] ReadRawCompressedData(byte[] packageBytes, string entryName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(entryName);

        using var ms = new MemoryStream(packageBytes);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        while (ms.Position < packageBytes.Length - 30)
        {
            var startPos = ms.Position;
            if (reader.ReadUInt32() != 0x04034b50)
            {
                ms.Position = startPos + 1;
                continue;
            }

            reader.ReadInt16(); // version needed
            var flags = reader.ReadInt16();
            reader.ReadInt16(); // compression method
            reader.ReadInt32(); // mod time/date
            reader.ReadUInt32(); // crc32
            reader.ReadUInt32(); // compressed size (0 when data descriptor used)
            reader.ReadUInt32(); // uncompressed size
            var nameLen = reader.ReadInt16();
            var extraLen = reader.ReadInt16();

            var headerName = reader.ReadBytes(nameLen);
            ms.Position += extraLen;

            if (headerName.Length == nameBytes.Length && headerName.SequenceEqual(nameBytes))
            {
                // Data descriptor flag (bit 3) means sizes are after data.
                // We need CompressedLength from the central directory.
                // Use ZipArchive to get it.
                using var zipMs = new MemoryStream(packageBytes);
                using var zip = new ZipArchive(zipMs, ZipArchiveMode.Read);
                var entry = zip.GetEntry(entryName)!;
                var dataLen = (int)entry.CompressedLength;

                var data = new byte[dataLen];
                var dataOffset = ms.Position;
                Array.Copy(packageBytes, dataOffset, data, 0, dataLen);
                return data;
            }

            // Skip to next header (use central directory approach)
            ms.Position = startPos + 4;
        }

        throw new InvalidOperationException($"Local header not found for {entryName}");
    }
}
