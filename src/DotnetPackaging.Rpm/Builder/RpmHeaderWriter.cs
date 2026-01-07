using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmHeaderWriter
{
    public static byte[] BuildMetadataHeader(PackageMetadata metadata, RpmFileList fileList, int payloadSize, byte[] compressedPayload)
    {
        var summary = ResolveSummary(metadata);
        var description = ResolveDescription(metadata, summary);
        var buildTime = (int)metadata.ModificationTime.ToUnixTimeSeconds();
        var installedSize = metadata.InstalledSize.GetValueOrDefault(fileList.Entries.Sum(entry => (long)entry.Size));
        var sizeValue = (int)Math.Min(installedSize, int.MaxValue);
        var entries = fileList.Entries;
        var fileCount = entries.Count;

        var headerEntries = new List<RpmHeaderEntry>
        {
            RpmHeaderEntry.StringArray(RpmTag.HeaderI18nTable, new[] { "C" }),
            RpmHeaderEntry.String(RpmTag.Name, metadata.Package),
            RpmHeaderEntry.String(RpmTag.Version, metadata.Version),
            RpmHeaderEntry.String(RpmTag.Release, "1"),
            RpmHeaderEntry.I18nString(RpmTag.Summary, summary),
            RpmHeaderEntry.I18nString(RpmTag.Description, description),
            RpmHeaderEntry.Int32(RpmTag.BuildTime, buildTime),
            RpmHeaderEntry.String(RpmTag.BuildHost, Environment.MachineName),
            RpmHeaderEntry.Int32(RpmTag.Size, sizeValue),
            RpmHeaderEntry.String(RpmTag.License, metadata.License.GetValueOrDefault("Proprietary")),
            RpmHeaderEntry.String(RpmTag.Os, "linux"),
            RpmHeaderEntry.String(RpmTag.Arch, MapArchitecture(metadata.Architecture)),
            RpmHeaderEntry.Int32(RpmTag.ArchiveSize, payloadSize),
            RpmHeaderEntry.String(RpmTag.PayloadFormat, "cpio"),
            RpmHeaderEntry.String(RpmTag.PayloadCompressor, "gzip"),
            RpmHeaderEntry.Int32(RpmTag.FileDigestAlgo, 8), // SHA-256
            RpmHeaderEntry.StringArray(RpmTag.PayloadDigest, new[] { ComputeSha256Hex(compressedPayload) }),
            RpmHeaderEntry.Int32(RpmTag.PayloadDigestAlgo, 8), // SHA-256
            metadata.Vendor.Map(v => RpmHeaderEntry.String(RpmTag.Vendor, v)).GetValueOrDefault(RpmHeaderEntry.String(RpmTag.Vendor, "Unknown")),
            metadata.Url.Map(u => RpmHeaderEntry.String(RpmTag.Url, u.ToString())).GetValueOrDefault(RpmHeaderEntry.String(RpmTag.Url, "http://localhost")),
            RpmHeaderEntry.CharArray(RpmTag.FileStates, new byte[fileCount]),
            RpmHeaderEntry.Int32Array(RpmTag.FileSizes, entries.Select(entry => entry.Size).ToArray()),
            RpmHeaderEntry.Int16Array(RpmTag.FileModes, entries.Select(entry => unchecked((short)entry.Mode)).ToArray()),
            RpmHeaderEntry.Int16Array(RpmTag.FileRdevs, new short[fileCount]),
            RpmHeaderEntry.Int32Array(RpmTag.FileMtimes, entries.Select(entry => entry.MTime).ToArray()),
            RpmHeaderEntry.StringArray(RpmTag.FileDigests, entries.Select(entry => entry.Digest).ToArray()),
            RpmHeaderEntry.StringArray(RpmTag.FileLinkTos, Enumerable.Repeat(string.Empty, fileCount).ToArray()),
            RpmHeaderEntry.Int32Array(RpmTag.FileFlags, new int[fileCount]),
            RpmHeaderEntry.StringArray(RpmTag.FileUserName, entries.Select(entry => entry.UserName).ToArray()),
            RpmHeaderEntry.StringArray(RpmTag.FileGroupName, entries.Select(entry => entry.GroupName).ToArray()),
            RpmHeaderEntry.Int32Array(RpmTag.FileVerifyFlags, new int[fileCount]),
            RpmHeaderEntry.Int32Array(RpmTag.FileDevices, new int[fileCount]),
            RpmHeaderEntry.Int32Array(RpmTag.FileInodes, entries.Select(entry => entry.Inode).ToArray()),
            RpmHeaderEntry.StringArray(RpmTag.FileLangs, Enumerable.Repeat(string.Empty, fileCount).ToArray()),
            RpmHeaderEntry.Int32Array(RpmTag.DirIndexes, entries.Select(entry => entry.DirIndex).ToArray()),
            RpmHeaderEntry.StringArray(RpmTag.BaseNames, entries.Select(entry => entry.BaseName).ToArray()),
            RpmHeaderEntry.StringArray(RpmTag.DirNames, fileList.DirNames.ToArray())
        };

        return RpmHeaderBuilder.Build(headerEntries);
    }

    public static byte[] BuildSignatureHeader(byte[] header, byte[] payload)
    {
        var headerPayload = Combine(header, payload);
        var size = headerPayload.Length;

        // Calculate checksums
        var md5 = MD5.HashData(headerPayload);
        var sha1Header = SHA1.HashData(header);
        var sha256Header = SHA256.HashData(header);

        // Convert to hex strings (lowercase as per RPM convention)
        var sha1String = Convert.ToHexString(sha1Header).ToLowerInvariant();
        var sha256String = Convert.ToHexString(sha256Header).ToLowerInvariant();

        var entries = new List<RpmHeaderEntry>
        {
            RpmHeaderEntry.Int32(RpmSignatureTag.Size, size),
            RpmHeaderEntry.Bin(RpmSignatureTag.Md5, md5),
            RpmHeaderEntry.String(RpmSignatureTag.Sha1Header, sha1String),
            RpmHeaderEntry.String(RpmSignatureTag.Sha256Header, sha256String)
        };

        return RpmHeaderBuilder.Build(entries);
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static string ResolveSummary(PackageMetadata metadata)
    {
        var summary = metadata.Summary.GetValueOrDefault(metadata.Comment.GetValueOrDefault(metadata.Name));
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = metadata.Name;
        }

        return Sanitize(summary);
    }

    private static string ResolveDescription(PackageMetadata metadata, string fallback)
    {
        var description = metadata.Description.GetValueOrDefault(metadata.Comment.GetValueOrDefault(fallback));
        return string.IsNullOrWhiteSpace(description) ? fallback : description;
    }

    private static string Sanitize(string value)
    {
        return value.Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string MapArchitecture(Architecture architecture)
    {
        if (architecture == Architecture.All)
        {
            return "noarch";
        }

        if (architecture == Architecture.X64)
        {
            return "x86_64";
        }

        if (architecture == Architecture.X86)
        {
            return "i386";
        }

        if (architecture == Architecture.Arm64)
        {
            return "aarch64";
        }

        if (architecture == Architecture.Arm32)
        {
            return "armv7hl";
        }

        return architecture.PackagePrefix;
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] ComputeSha256(byte[] data)
    {
        return SHA256.HashData(data);
    }
}

internal enum RpmTagType
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

internal readonly record struct RpmHeaderEntry(int Tag, RpmTagType Type, int Count, byte[] Data)
{
    public static RpmHeaderEntry String(int tag, string value)
    {
        var data = EncodeString(value);
        return new RpmHeaderEntry(tag, RpmTagType.String, 1, data);
    }

    public static RpmHeaderEntry StringArray(int tag, IReadOnlyCollection<string> values)
    {
        var data = EncodeStringArray(values);
        return new RpmHeaderEntry(tag, RpmTagType.StringArray, values.Count, data);
    }

    public static RpmHeaderEntry I18nString(int tag, string value)
    {
        var data = EncodeStringArray(new[] { value });
        return new RpmHeaderEntry(tag, RpmTagType.I18NString, 1, data);
    }

    public static RpmHeaderEntry Int32(int tag, int value)
    {
        var data = EncodeInt32Array(new[] { value });
        return new RpmHeaderEntry(tag, RpmTagType.Int32, 1, data);
    }

    public static RpmHeaderEntry Int32Array(int tag, IReadOnlyCollection<int> values)
    {
        var data = EncodeInt32Array(values);
        return new RpmHeaderEntry(tag, RpmTagType.Int32, values.Count, data);
    }

    public static RpmHeaderEntry Int16Array(int tag, IReadOnlyCollection<short> values)
    {
        var data = EncodeInt16Array(values);
        return new RpmHeaderEntry(tag, RpmTagType.Int16, values.Count, data);
    }

    public static RpmHeaderEntry CharArray(int tag, IReadOnlyCollection<byte> values)
    {
        return new RpmHeaderEntry(tag, RpmTagType.Char, values.Count, values.ToArray());
    }

    public static RpmHeaderEntry Bin(int tag, byte[] value)
    {
        return new RpmHeaderEntry(tag, RpmTagType.Bin, value.Length, value);
    }

    public int Alignment => Type switch
    {
        RpmTagType.Int16 => 2,
        RpmTagType.Int32 => 4,
        RpmTagType.Int64 => 8,
        _ => 1
    };

    private static byte[] EncodeString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var buffer = new byte[bytes.Length + 1];
        bytes.CopyTo(buffer, 0);
        return buffer;
    }

    private static byte[] EncodeStringArray(IEnumerable<string> values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
        }

        return stream.ToArray();
    }

    private static byte[] EncodeInt32Array(IEnumerable<int> values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        return stream.ToArray();
    }

    private static byte[] EncodeInt16Array(IEnumerable<short> values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
        {
            Span<byte> buffer = stackalloc byte[2];
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            stream.Write(buffer);
        }

        return stream.ToArray();
    }
}

internal static class RpmHeaderBuilder
{
    private static readonly byte[] HeaderMagic = { 0x8E, 0xAD, 0xE8, 0x01 };

    public static byte[] Build(IReadOnlyCollection<RpmHeaderEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Tag).ToArray();
        using var data = new MemoryStream();
        var indexEntries = new List<RpmHeaderIndexEntry>(ordered.Length);

        foreach (var entry in ordered)
        {
            var aligned = Align((int)data.Length, entry.Alignment);
            if (aligned > data.Length)
            {
                data.Write(new byte[aligned - data.Length]);
            }

            indexEntries.Add(new RpmHeaderIndexEntry(entry.Tag, entry.Type, aligned, entry.Count));
            data.Write(entry.Data, 0, entry.Data.Length);
        }

        using var header = new MemoryStream();
        header.Write(HeaderMagic, 0, HeaderMagic.Length);
        header.Write(new byte[4], 0, 4);
        WriteInt32(header, ordered.Length);
        WriteInt32(header, (int)data.Length);

        foreach (var indexEntry in indexEntries)
        {
            WriteInt32(header, indexEntry.Tag);
            WriteInt32(header, (int)indexEntry.Type);
            WriteInt32(header, indexEntry.Offset);
            WriteInt32(header, indexEntry.Count);
        }

        var dataBytes = data.ToArray();
        header.Write(dataBytes, 0, dataBytes.Length);
        return header.ToArray();
    }

    private static int Align(int offset, int alignment)
    {
        if (alignment <= 1)
        {
            return offset;
        }

        var remainder = offset % alignment;
        return remainder == 0 ? offset : offset + (alignment - remainder);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}

internal readonly record struct RpmHeaderIndexEntry(int Tag, RpmTagType Type, int Offset, int Count);

internal static class RpmTag
{
    public const int HeaderI18nTable = 100;
    public const int Name = 1000;
    public const int Version = 1001;
    public const int Release = 1002;
    public const int Summary = 1004;
    public const int Description = 1005;
    public const int BuildTime = 1006;
    public const int BuildHost = 1007;
    public const int Size = 1009;
    public const int License = 1014;
    public const int FileStates = 1029;
    public const int FileSizes = 1028;
    public const int FileModes = 1030;
    public const int FileRdevs = 1033;
    public const int FileMtimes = 1034;
    public const int FileDigests = 1035;
    public const int FileLinkTos = 1036;
    public const int FileFlags = 1037;
    public const int FileUserName = 1039;
    public const int FileGroupName = 1040;
    public const int FileVerifyFlags = 1045;
    public const int ArchiveSize = 1046;
    public const int Os = 1021;
    public const int Arch = 1022;
    public const int FileDevices = 1095;
    public const int FileInodes = 1096;
    public const int FileLangs = 1097;
    public const int DirIndexes = 1116;
    public const int BaseNames = 1117;
    public const int DirNames = 1118;
    public const int PayloadFormat = 1124;
    public const int PayloadCompressor = 1125;
    public const int FileDigestAlgo = 5011;
    public const int PayloadDigest = 5092;
    public const int PayloadDigestAlgo = 5093;
    public const int Vendor = 1011;
    public const int Url = 1020;
}

internal static class RpmSignatureTag
{
    public const int Size = 257;
    public const int Md5 = 261;
    public const int Sha1Header = 269;
    public const int Sha256Header = 273;
}
