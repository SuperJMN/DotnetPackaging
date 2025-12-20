using System.Buffers.Binary;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DotnetPackaging.Formats.Dmg.Udif;

public sealed class UdifImage
{
    private const int SectorSize = 512;
    private readonly string path;
    private readonly BlkxTable blkx;

    private UdifImage(string path, UdifTrailer trailer, BlkxTable blkx)
    {
        this.path = path;
        Trailer = trailer;
        this.blkx = blkx;
    }

    public UdifTrailer Trailer { get; }

    public IReadOnlyList<BlkxRun> Runs => blkx.Runs;

    public static async Task<UdifImage> Load(string dmgPath)
    {
        await using var stream = File.OpenRead(dmgPath);
        if (stream.Length < SectorSize)
        {
            throw new InvalidDataException("File too small to be a DMG");
        }

        stream.Seek(-SectorSize, SeekOrigin.End);
        var trailerBytes = new byte[SectorSize];
        await stream.ReadExactlyAsync(trailerBytes, 0, trailerBytes.Length);
        var trailer = UdifTrailer.Parse(trailerBytes);

        var xmlOffset = checked((long)trailer.XmlOffset);
        var xmlLength = checked((int)trailer.XmlLength);
        var xmlRegionEnd = xmlOffset + xmlLength;
        if (xmlOffset < 0 || xmlRegionEnd > stream.Length - SectorSize)
        {
            throw new InvalidDataException("XML section is outside the DMG bounds");
        }

        if (trailer.DataForkLength != (ulong)xmlOffset)
        {
            throw new InvalidDataException("Data fork length does not match XML offset");
        }

        stream.Seek(xmlOffset, SeekOrigin.Begin);
        var xmlBytes = new byte[xmlLength];
        await stream.ReadExactlyAsync(xmlBytes, 0, xmlBytes.Length);
        var blkxBytes = ExtractFirstBlkx(xmlBytes);
        var blkx = BlkxTable.Parse(blkxBytes);

        return new UdifImage(dmgPath, trailer, blkx);
    }

    public async Task<byte[]> ExtractDataFork()
    {
        var totalSectors = blkx.Runs.Where(r => r.Type != BlkxRunType.Terminator)
            .Aggregate(0UL, (current, run) => current + run.SectorCount);
        var totalBytes = checked((int)(totalSectors * SectorSize));
        var output = new byte[totalBytes];

        await using var stream = File.OpenRead(path);
        foreach (var run in blkx.Runs.Where(r => r.Type != BlkxRunType.Terminator))
        {
            if (run.CompLength == 0)
            {
                continue;
            }

            stream.Seek(checked((long)(blkx.DataStart + run.CompOffset)), SeekOrigin.Begin);
            var compressed = new byte[checked((int)run.CompLength)];
            await stream.ReadExactlyAsync(compressed, 0, compressed.Length);

            var targetOffset = checked((int)(run.SectorStart * SectorSize));
            var targetLength = checked((int)(run.SectorCount * SectorSize));
            var target = output.AsSpan(targetOffset, targetLength);

            switch (run.Type)
            {
                case BlkxRunType.Raw:
                    compressed.AsSpan().CopyTo(target);
                    break;
                case BlkxRunType.Zlib:
                    DecompressZlib(compressed, target);
                    break;
                case BlkxRunType.Zero:
                    target.Clear();
                    break;
                default:
                    throw new InvalidDataException($"Unsupported blkx run type: 0x{((uint)run.Type):X}");
            }
        }

        // Modern DMG created by UdifWriter doesn't have legacy APM overhead
        // Just return the full extracted volume (raw HFS+)
        return output;
    }

    private static byte[] ExtractFirstBlkx(byte[] xmlBytes)
    {
        using var reader = new StringReader(Encoding.UTF8.GetString(xmlBytes));
        var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore };
        using var xmlReader = System.Xml.XmlReader.Create(reader, settings);
        var document = XDocument.Load(xmlReader);

        var blkxKey = document
            .Descendants("key")
            .FirstOrDefault(k => string.Equals(k.Value, "blkx", StringComparison.Ordinal));

        if (blkxKey == null)
        {
            throw new InvalidDataException("blkx resource not found in plist");
        }

        var blkxArray = blkxKey.ElementsAfterSelf().FirstOrDefault(e => e.Name.LocalName == "array");
        if (blkxArray == null)
        {
            throw new InvalidDataException("blkx array missing");
        }

        var firstDataElement = blkxArray
            .Descendants("data")
            .FirstOrDefault();

        if (firstDataElement == null)
        {
            throw new InvalidDataException("blkx data element missing");
        }

        var base64 = firstDataElement.Value;
        var decoded = Convert.FromBase64String(base64);
        return decoded;
    }

    private static void DecompressZlib(ReadOnlySpan<byte> compressed, Span<byte> output)
    {
        using var compressedStream = new MemoryStream(compressed.ToArray());
        using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
        var totalRead = 0;
        while (totalRead < output.Length)
        {
            var read = zlib.Read(output[totalRead..]);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }

        if (totalRead != output.Length)
        {
            throw new InvalidDataException("Unexpected end of zlib stream");
        }
    }
}

public record UdifTrailer(
    string Signature,
    uint Version,
    uint HeaderSize,
    uint Flags,
    ulong DataForkOffset,
    ulong DataForkLength,
    ulong XmlOffset,
    ulong XmlLength,
    uint ImageVariant,
    ulong SectorCount)
{
    public static UdifTrailer Parse(ReadOnlySpan<byte> data)
    {
        var signature = Encoding.ASCII.GetString(data[..4]);
        if (signature != "koly")
        {
            throw new InvalidDataException("UDIF trailer signature not found");
        }

        var version = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
        var headerSize = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
        if (headerSize != 512)
        {
            throw new InvalidDataException("Unexpected UDIF header size");
        }
        var flags = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4));
        var dataForkOffset = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(24, 8));
        var dataForkLength = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(32, 8));
        var xmlOffset = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(216, 8));
        var xmlLength = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(224, 8));
        var imageVariant = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(488, 4));
        var sectorCount = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(492, 8));

        return new UdifTrailer(signature, version, headerSize, flags, dataForkOffset, dataForkLength, xmlOffset, xmlLength, imageVariant, sectorCount);
    }
}

public enum BlkxRunType : uint
{
    Raw = 0x00000001,
    Ignore = 0x00000002,
    Zlib = 0x80000005,
    Terminator = 0xFFFFFFFF,
    Zero = 0x00000000
}

public record BlkxRun(BlkxRunType Type, ulong SectorStart, ulong SectorCount, ulong CompOffset, ulong CompLength);

public record BlkxTable(
    uint Signature,
    uint InfoVersion,
    ulong FirstSectorNumber,
    ulong SectorCount,
    ulong DataStart,
    uint DecompressBufferRequested,
    uint BlocksDescriptor,
    UdifChecksum Checksum,
    IReadOnlyList<BlkxRun> Runs)
{
    private const int HeaderSize = 204;
    public static BlkxTable Parse(ReadOnlySpan<byte> data)
    {
        var signature = BinaryPrimitives.ReadUInt32BigEndian(data);
        if (signature != 0x6D697368) // 'mish'
        {
            throw new InvalidDataException("blkx signature missing");
        }

        var infoVersion = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
        var firstSectorNumber = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(8, 8));
        var sectorCount = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(16, 8));
        var dataStart = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(24, 8));
        var decompressBufferRequested = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(32, 4));
        var blocksDescriptor = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(36, 4));

        var checksum = UdifChecksum.Parse(data.Slice(64, 136));
        var blocksRunCount = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(200, 4));

        var runs = new List<BlkxRun>((int)blocksRunCount);
        var offset = 204;
        for (var i = 0; i < blocksRunCount; i++)
        {
            var type = (BlkxRunType)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            _ = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 4, 4));
            var sectorStart = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
            var runSectorCount = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 16, 8));
            var compOffset = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 24, 8));
            var compLength = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 32, 8));

            runs.Add(new BlkxRun(type, sectorStart, runSectorCount, compOffset, compLength));
            offset += 40;
        }

        return new BlkxTable(signature, infoVersion, firstSectorNumber, sectorCount, dataStart, decompressBufferRequested, blocksDescriptor, checksum, runs);
    }

    public byte[] ToBytes()
    {
        var buffer = new byte[HeaderSize + Runs.Count * 40];
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), Signature);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), InfoVersion);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(8, 8), FirstSectorNumber);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(16, 8), SectorCount);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(24, 8), DataStart);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(32, 4), DecompressBufferRequested);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(36, 4), BlocksDescriptor);
        // reserved1..6 already zeroed by array initialization

        var checksumBytes = Checksum.ToBytes();
        checksumBytes.CopyTo(buffer, 64);

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(200, 4), (uint)Runs.Count);

        var offset = 204;
        foreach (var run in Runs)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), (uint)run.Type);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 4, 4), 0);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(offset + 8, 8), run.SectorStart);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(offset + 16, 8), run.SectorCount);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(offset + 24, 8), run.CompOffset);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(offset + 32, 8), run.CompLength);
            offset += 40;
        }

        return buffer;
    }
}

public record UdifChecksum(uint Type, uint Size, IReadOnlyList<uint> Values)
{
    public static UdifChecksum Parse(ReadOnlySpan<byte> data)
    {
        var type = BinaryPrimitives.ReadUInt32BigEndian(data);
        var size = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
        var values = new uint[32];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8 + i * 4, 4));
        }
        return new UdifChecksum(type, size, values);
    }

    public byte[] ToBytes()
    {
        var buffer = new byte[136];
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), Type);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), Size);
        for (var i = 0; i < 32; i++)
        {
            var value = i < Values.Count ? Values[i] : 0;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8 + i * 4, 4), value);
        }

        return buffer;
    }
}
