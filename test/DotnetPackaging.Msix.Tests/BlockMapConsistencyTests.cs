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
    /// MSIX uses a single deflate stream per file with Z_SYNC_FLUSH between 64KB blocks.
    /// This test decompresses the entire compressed payload as one continuous stream,
    /// then splits into 64KB blocks and verifies SHA256 hashes from the block map.
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
                var rawCompressed = ReadRawCompressedData(packageBytes, fileName);
                uncompressed = DecompressEntireStream(rawCompressed);
            }
            else
            {
                uncompressed = ReadRawCompressedData(packageBytes, fileName);
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

    private static byte[] DecompressEntireStream(byte[] rawCompressed)
    {
        using var compMs = new MemoryStream(rawCompressed);
        using var deflate = new DeflateStream(compMs, CompressionMode.Decompress);
        using var result = new MemoryStream();
        deflate.CopyTo(result);
        return result.ToArray();
    }

    /// <summary>
    /// Block map compressed sizes sum should be ≤ ZIP compressed length.
    /// The difference is the Z_FINISH terminator bytes (not tracked in the block map).
    /// </summary>
    [Fact]
    public async Task Block_compressed_sizes_sum_is_less_than_or_equal_to_zip_compressed_length()
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

            Assert.True(blockMapTotal <= zipEntry.CompressedLength,
                $"Compressed size inconsistency for {fileName}: " +
                $"block map says {blockMapTotal}, ZIP says {zipEntry.CompressedLength}. " +
                $"Block map total should be ≤ ZIP compressed (difference is Z_FINISH bytes)");
        }
    }

    /// <summary>
    /// The entire compressed payload for each file forms a single valid deflate stream
    /// (blocks use Z_SYNC_FLUSH boundaries, not independent streams).
    /// Decompressing the whole stream should yield data whose size matches the block map.
    /// </summary>
    [Fact]
    public async Task Entire_compressed_stream_decompresses_to_expected_size()
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
            if (!isCompressed) continue;

            var rawCompressed = ReadRawCompressedData(packageBytes, fileName);
            var decompressed = DecompressEntireStream(rawCompressed);

            Assert.True(decompressed.Length == declaredSize,
                $"Decompressed size mismatch for {fileName}: " +
                $"expected {declaredSize}, got {decompressed.Length}");
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
