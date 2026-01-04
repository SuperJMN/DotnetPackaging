using System.Buffers.Binary;

namespace DotnetPackaging.Rpm.Tests;

internal static class RpmArchiveReader
{
    private static readonly byte[] LeadMagic = { 0xED, 0xAB, 0xEE, 0xDB };
    private static readonly byte[] HeaderMagic = { 0x8E, 0xAD, 0xE8, 0x01 };

    public static RpmArchive Read(byte[] rpmBytes)
    {
        if (rpmBytes.Length < 96)
        {
            throw new InvalidDataException("RPM file is too small to contain a lead.");
        }

        ValidateLead(rpmBytes);
        var offset = 96;
        offset = ReadHeaderAt(rpmBytes, offset, out _);
        offset = Align(offset, 8);
        offset = ReadHeaderAt(rpmBytes, offset, out var header);
        var payload = rpmBytes[offset..];
        return new RpmArchive(header, payload);
    }

    private static void ValidateLead(byte[] rpmBytes)
    {
        for (var i = 0; i < LeadMagic.Length; i++)
        {
            if (rpmBytes[i] != LeadMagic[i])
            {
                throw new InvalidDataException("Invalid RPM lead magic.");
            }
        }
    }

    private static int ReadHeaderAt(byte[] rpmBytes, int offset, out RpmHeader header)
    {
        var magic = rpmBytes.AsSpan(offset, 4);
        if (!magic.SequenceEqual(HeaderMagic))
        {
            throw new InvalidDataException("Invalid RPM header magic.");
        }

        var entryCount = ReadInt32(rpmBytes.AsSpan(offset + 8, 4));
        var dataSize = ReadInt32(rpmBytes.AsSpan(offset + 12, 4));
        var indexStart = offset + 16;
        var dataStart = indexStart + (entryCount * 16);
        var dataEnd = dataStart + dataSize;
        if (dataEnd > rpmBytes.Length)
        {
            throw new InvalidDataException("RPM header data exceeds file size.");
        }

        var data = rpmBytes.AsSpan(dataStart, dataSize);
        var tags = new Dictionary<int, RpmTagValue>();

        for (var i = 0; i < entryCount; i++)
        {
            var indexOffset = indexStart + (i * 16);
            var tag = ReadInt32(rpmBytes.AsSpan(indexOffset, 4));
            var type = (RpmTagType)ReadInt32(rpmBytes.AsSpan(indexOffset + 4, 4));
            var valueOffset = ReadInt32(rpmBytes.AsSpan(indexOffset + 8, 4));
            var count = ReadInt32(rpmBytes.AsSpan(indexOffset + 12, 4));
            var value = ReadValue(type, data, valueOffset, count);
            tags[tag] = new RpmTagValue(type, value);
        }

        header = new RpmHeader(tags);
        return dataEnd;
    }

    private static object ReadValue(RpmTagType type, ReadOnlySpan<byte> data, int offset, int count)
    {
        return type switch
        {
            RpmTagType.String => ReadString(data, offset),
            RpmTagType.StringArray or RpmTagType.I18NString => ReadStringArray(data, offset, count),
            RpmTagType.Int32 => ReadInt32Array(data, offset, count),
            RpmTagType.Int16 => ReadInt16Array(data, offset, count),
            RpmTagType.Char => ReadByteArray(data, offset, count),
            RpmTagType.Bin => ReadByteArray(data, offset, count),
            _ => ReadByteArray(data, offset, count)
        };
    }

    private static string ReadString(ReadOnlySpan<byte> data, int offset)
    {
        var span = data[offset..];
        var length = span.IndexOf((byte)0);
        if (length < 0)
        {
            length = span.Length;
        }

        return Encoding.UTF8.GetString(span[..length]);
    }

    private static string[] ReadStringArray(ReadOnlySpan<byte> data, int offset, int count)
    {
        var result = new string[count];
        var position = offset;
        for (var i = 0; i < count; i++)
        {
            var span = data[position..];
            var length = span.IndexOf((byte)0);
            if (length < 0)
            {
                length = span.Length;
            }

            result[i] = Encoding.UTF8.GetString(span[..length]);
            position += length + 1;
        }

        return result;
    }

    private static int[] ReadInt32Array(ReadOnlySpan<byte> data, int offset, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = ReadInt32(data.Slice(offset + (i * 4), 4));
        }

        return values;
    }

    private static short[] ReadInt16Array(ReadOnlySpan<byte> data, int offset, int count)
    {
        var values = new short[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = ReadInt16(data.Slice(offset + (i * 2), 2));
        }

        return values;
    }

    private static byte[] ReadByteArray(ReadOnlySpan<byte> data, int offset, int count)
    {
        return data.Slice(offset, count).ToArray();
    }

    private static int ReadInt32(ReadOnlySpan<byte> buffer)
    {
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    private static short ReadInt16(ReadOnlySpan<byte> buffer)
    {
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    private static int Align(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}

public sealed record RpmArchive(RpmHeader Header, byte[] Payload);

public sealed class RpmHeader
{
    private readonly IReadOnlyDictionary<int, RpmTagValue> tags;

    public RpmHeader(IReadOnlyDictionary<int, RpmTagValue> tags)
    {
        this.tags = tags;
    }

    public string GetString(int tag) => (string)tags[tag].Value;

    public string[] GetStringArray(int tag) => (string[])tags[tag].Value;

    public int[] GetInt32Array(int tag) => (int[])tags[tag].Value;

    public short[] GetInt16Array(int tag) => (short[])tags[tag].Value;

    public byte[] GetByteArray(int tag) => (byte[])tags[tag].Value;
}

public readonly record struct RpmTagValue(RpmTagType Type, object Value);

public enum RpmTagType
{
    Null = 0,
    Char = 1,
    Int8 = 2,
    Int16 = 3,
    Int32 = 4,
    Int64 = 5,
    String = 6,
    Bin = 7,
    StringArray = 8,
    I18NString = 9
}
