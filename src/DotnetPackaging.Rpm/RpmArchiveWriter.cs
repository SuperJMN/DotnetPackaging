using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Rpm;

internal static class RpmArchiveWriter
{
    public static IData Write(RpmPackage package)
    {
        var orderedEntries = package.Entries
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToList();

        var files = orderedEntries.Select((entry, index) => BuildFileRecord(entry, index)).ToList();

        var (cpio, uncompressedSize) = BuildPayload(files);

        var architecture = MapArchitecture(package.Metadata.Architecture);
        var lead = BuildLead(package, architecture);

        var header = BuildHeader(package, files, architecture, cpio.Length);

        var signature = BuildSignature(header, cpio, uncompressedSize);

        using var stream = new MemoryStream(lead.Length + signature.Length + header.Length + cpio.Length);
        stream.Write(lead);
        stream.Write(signature);
        stream.Write(header);
        stream.Write(cpio);

        return Data.FromByteArray(stream.ToArray());
    }

    private static FileRecord BuildFileRecord(RpmEntry entry, int index)
    {
        var normalized = NormalizePath(entry.Path);
        var (directory, baseName) = SplitPath(normalized);
        var permissions = (int)entry.Properties.FileMode;
        var typeBits = entry.Type == RpmEntryType.Directory ? FileTypeBits.Directory : FileTypeBits.RegularFile;
        var mode = typeBits | permissions;
        var ownerId = entry.Properties.OwnerId.GetValueOrDefault(0);
        var groupId = entry.Properties.GroupId.GetValueOrDefault(0);
        var ownerName = entry.Properties.OwnerUsername.GetValueOrDefault("root");
        var groupName = entry.Properties.GroupName.GetValueOrDefault("root");
        var contentBytes = entry.Content?.Bytes() ?? Array.Empty<byte>();
        var digest = ComputeMd5(contentBytes);
        var mtime = (int)entry.Properties.LastModification.ToUnixTimeSeconds();
        var size = entry.Type == RpmEntryType.Directory ? 0 : contentBytes.Length;
        var cpioPath = BuildPayloadPath(normalized, entry.Type);

        return new FileRecord(
            Index: index,
            NormalizedPath: normalized,
            Directory: directory,
            BaseName: baseName,
            Mode: mode,
            Size: size,
            OwnerId: ownerId,
            GroupId: groupId,
            OwnerName: ownerName,
            GroupName: groupName,
            Digest: digest,
            MTime: mtime,
            PayloadPath: cpioPath,
            Content: contentBytes,
            EntryType: entry.Type);
    }

    private static (byte[] Payload, int UncompressedSize) BuildPayload(IReadOnlyList<FileRecord> files)
    {
        var uncompressed = BuildCpio(files);
        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(uncompressed, 0, uncompressed.Length);
        }

        return (compressedStream.ToArray(), uncompressed.Length);
    }

    private static byte[] BuildHeader(
        RpmPackage package,
        IReadOnlyList<FileRecord> files,
        string architecture,
        int archiveSize)
    {
        var summary = Sanitize(package.Metadata.Summary.GetValueOrDefault(
            package.Metadata.Comment.GetValueOrDefault(package.Metadata.Name)));
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = package.Metadata.Name;
        }

        var description = package.Metadata.Description
            .GetValueOrDefault(package.Metadata.Comment.GetValueOrDefault(summary));
        var license = package.Metadata.License.GetValueOrDefault("Proprietary");
        var url = package.Metadata.Homepage.Map(uri => uri.ToString()).GetValueOrDefault("https://example.com");
        var vendor = package.Metadata.Maintainer.GetValueOrDefault(package.Metadata.Name);
        var group = package.Metadata.Section.GetValueOrDefault("Applications/Unknown");
        var buildHost = Environment.MachineName;
        var buildTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var archivePayloadSize = files.Where(f => f.EntryType == RpmEntryType.File).Sum(f => f.Size);

        var dirNames = files
            .Select(f => f.Directory)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(d => d, StringComparer.Ordinal)
            .Select(EnsureTrailingSlash)
            .ToList();

        var dirIndexes = files
            .Select(f => dirNames.IndexOf(EnsureTrailingSlash(f.Directory)))
            .ToArray();

        var basenames = files.Select(f => f.BaseName).ToArray();
        var fileSizes = files.Select(f => f.Size).ToArray();
        var fileStates = files.Select(_ => (byte)0).ToArray();
        var fileModes = files.Select(f => (short)f.Mode).ToArray();
        var fileUids = files.Select(f => f.OwnerId).ToArray();
        var fileGids = files.Select(f => f.GroupId).ToArray();
        var fileMtimes = files.Select(f => f.MTime).ToArray();
        var fileDigests = files.Select(f => f.Digest).ToArray();
        var fileLinkTos = files.Select(_ => string.Empty).ToArray();
        var fileFlags = files.Select(f => f.EntryType == RpmEntryType.Directory ? (int)RpmFileFlags.Directory : (int)RpmFileFlags.None).ToArray();
        var fileUserNames = files.Select(f => f.OwnerName).ToArray();
        var fileGroupNames = files.Select(f => f.GroupName).ToArray();
        var fileVerifyFlags = files.Select(_ => (int)RpmVerifyFlags.Default).ToArray();
        var fileDevices = files.Select(_ => 0).ToArray();
        var fileRDevices = files.Select(_ => 0).ToArray();
        var fileInodes = files.Select((_, idx) => idx + 1).ToArray();
        var fileLangs = files.Select(_ => string.Empty).ToArray();

        var entries = new List<HeaderEntry>
        {
            HeaderEntry.StringArray(RpmTags.HeaderI18NTable, new[] { "C" }),
            HeaderEntry.String(RpmTags.Name, package.Metadata.Package),
            HeaderEntry.String(RpmTags.Version, package.Metadata.Version),
            HeaderEntry.String(RpmTags.Release, "1"),
            HeaderEntry.I18NString(RpmTags.Summary, summary),
            HeaderEntry.I18NString(RpmTags.Description, description),
            HeaderEntry.Int32(RpmTags.BuildTime, buildTime),
            HeaderEntry.String(RpmTags.BuildHost, buildHost),
            HeaderEntry.Int32(RpmTags.Size, archivePayloadSize),
            HeaderEntry.String(RpmTags.License, license),
            HeaderEntry.String(RpmTags.Group, group),
            HeaderEntry.String(RpmTags.Url, url),
            HeaderEntry.String(RpmTags.Os, "linux"),
            HeaderEntry.String(RpmTags.Arch, architecture),
            HeaderEntry.String(RpmTags.Vendor, vendor),
            HeaderEntry.String(RpmTags.Packager, vendor),
            HeaderEntry.String(RpmTags.PayloadFormat, "cpio"),
            HeaderEntry.String(RpmTags.PayloadCompressor, "gzip"),
            HeaderEntry.String(RpmTags.PayloadFlags, "9"),
            HeaderEntry.Int32(RpmTags.ArchiveSize, archiveSize),
            HeaderEntry.StringArray(RpmTags.DirNames, dirNames.ToArray()),
            HeaderEntry.Int32Array(RpmTags.DirIndexes, dirIndexes),
            HeaderEntry.StringArray(RpmTags.Basenames, basenames),
            HeaderEntry.Int32Array(RpmTags.FileSizes, fileSizes),
            HeaderEntry.Int8Array(RpmTags.FileStates, fileStates),
            HeaderEntry.Int16Array(RpmTags.FileModes, fileModes),
            HeaderEntry.Int32Array(RpmTags.FileUids, fileUids),
            HeaderEntry.Int32Array(RpmTags.FileGids, fileGids),
            HeaderEntry.Int32Array(RpmTags.FileMtimes, fileMtimes),
            HeaderEntry.StringArray(RpmTags.FileDigests, fileDigests),
            HeaderEntry.StringArray(RpmTags.FileLinkTos, fileLinkTos),
            HeaderEntry.Int32Array(RpmTags.FileFlags, fileFlags),
            HeaderEntry.StringArray(RpmTags.FileUserNames, fileUserNames),
            HeaderEntry.StringArray(RpmTags.FileGroupNames, fileGroupNames),
            HeaderEntry.Int32Array(RpmTags.FileVerifyFlags, fileVerifyFlags),
            HeaderEntry.Int32Array(RpmTags.FileDevices, fileDevices),
            HeaderEntry.Int32Array(RpmTags.FileRDevices, fileRDevices),
            HeaderEntry.Int32Array(RpmTags.FileInodes, fileInodes),
            HeaderEntry.StringArray(RpmTags.FileLangs, fileLangs),
            HeaderEntry.Int32(RpmTags.FileDigestAlgorithm, (int)RpmDigestAlgorithm.Md5),
        };

        return BuildHeaderBlock(entries);
    }

    private static byte[] BuildSignature(byte[] header, byte[] payload, int payloadSize)
    {
        var concatenated = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, concatenated, 0, header.Length);
        Buffer.BlockCopy(payload, 0, concatenated, header.Length, payload.Length);

        var md5 = MD5.HashData(concatenated);
        var sha256 = SHA256.HashData(concatenated);

        var entries = new List<HeaderEntry>
        {
            HeaderEntry.Int32(RpmSignatureTags.Size, 0), // placeholder, updated once we know the signature size
            HeaderEntry.Int32(RpmSignatureTags.PayloadSize, payloadSize),
            HeaderEntry.Bin(RpmSignatureTags.Md5, md5),
            HeaderEntry.Bin(RpmSignatureTags.Sha256, sha256),
        };

        var signature = BuildHeaderBlock(entries);
        var totalSize = header.Length + payload.Length + signature.Length;

        entries[0] = HeaderEntry.Int32(RpmSignatureTags.Size, totalSize);
        return BuildHeaderBlock(entries);
    }

    private static byte[] BuildHeaderBlock(IReadOnlyList<HeaderEntry> entries)
    {
        var indexRecords = new List<HeaderIndex>();
        using var storeStream = new MemoryStream();

        foreach (var entry in entries)
        {
            var dataAndCount = SerializeEntry(entry, storeStream);
            indexRecords.Add(new HeaderIndex(entry.Tag, entry.Type, dataAndCount.Offset, dataAndCount.Count));
        }

        using var headerStream = new MemoryStream();
        headerStream.Write(HeaderMagic);
        WriteInt32(headerStream, 0);
        WriteInt32(headerStream, indexRecords.Count);
        WriteInt32(headerStream, (int)storeStream.Length);

        foreach (var index in indexRecords)
        {
            WriteInt32(headerStream, index.Tag);
            WriteInt32(headerStream, (int)index.Type);
            WriteInt32(headerStream, index.Offset);
            WriteInt32(headerStream, index.Count);
        }

        storeStream.Position = 0;
        storeStream.CopyTo(headerStream);

        while (headerStream.Length % 8 != 0)
        {
            headerStream.WriteByte(0);
        }

        return headerStream.ToArray();
    }

    private static SerializedEntry SerializeEntry(HeaderEntry entry, MemoryStream store)
    {
        var alignment = Alignment(entry.Type);
        AlignStream(store, alignment);

        var offset = (int)store.Position;
        var (count, data) = entry switch
        {
            { Type: HeaderValueType.Char } => SerializeChar(entry.Value),
            { Type: HeaderValueType.Int8 } => SerializeInt8(entry.Value),
            { Type: HeaderValueType.Int16 } => SerializeInt16(entry.Value),
            { Type: HeaderValueType.Int32 } => SerializeInt32(entry.Value),
            { Type: HeaderValueType.Int64 } => SerializeInt64(entry.Value),
            { Type: HeaderValueType.String } => SerializeString(entry.Value),
            { Type: HeaderValueType.Bin } => SerializeBin(entry.Value),
            { Type: HeaderValueType.StringArray } => SerializeStringArray(entry.Value),
            { Type: HeaderValueType.I18NString } => SerializeString(entry.Value),
            _ => throw new InvalidOperationException($"Unsupported header type {entry.Type}")
        };

        store.Write(data, 0, data.Length);
        return new SerializedEntry(offset, count);
    }

    private static (int Count, byte[] Data) SerializeChar(object value)
    {
        var bytes = value switch
        {
            byte[] array => array,
            byte b => new[] { b },
            _ => throw new InvalidOperationException("Invalid CHAR entry data")
        };

        return (bytes.Length, bytes);
    }

    private static (int Count, byte[] Data) SerializeInt8(object value)
    {
        var bytes = value switch
        {
            byte[] array => array,
            IEnumerable<byte> enumerable => enumerable.ToArray(),
            byte single => new[] { single },
            _ => throw new InvalidOperationException("Invalid INT8 entry data")
        };

        return (bytes.Length, bytes);
    }

    private static (int Count, byte[] Data) SerializeInt16(object value)
    {
        var values = value switch
        {
            short[] array => array,
            IEnumerable<short> enumerable => enumerable.ToArray(),
            short single => new[] { single },
            _ => throw new InvalidOperationException("Invalid INT16 entry data")
        };

        var buffer = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer.AsSpan(i * 2, 2), values[i]);
        }

        return (values.Length, buffer);
    }

    private static (int Count, byte[] Data) SerializeInt32(object value)
    {
        var values = value switch
        {
            int[] array => array,
            IEnumerable<int> enumerable => enumerable.ToArray(),
            int single => new[] { single },
            _ => throw new InvalidOperationException("Invalid INT32 entry data")
        };

        var buffer = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(i * 4, 4), values[i]);
        }

        return (values.Length, buffer);
    }

    private static (int Count, byte[] Data) SerializeInt64(object value)
    {
        var values = value switch
        {
            long[] array => array,
            IEnumerable<long> enumerable => enumerable.ToArray(),
            long single => new[] { single },
            _ => throw new InvalidOperationException("Invalid INT64 entry data")
        };

        var buffer = new byte[values.Length * 8];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(i * 8, 8), values[i]);
        }

        return (values.Length, buffer);
    }

    private static (int Count, byte[] Data) SerializeString(object value)
    {
        var text = value switch
        {
            string s => s,
            _ => throw new InvalidOperationException("Invalid STRING entry data")
        };

        var encoded = Encoding.UTF8.GetBytes(text);
        var buffer = new byte[encoded.Length + 1];
        Buffer.BlockCopy(encoded, 0, buffer, 0, encoded.Length);
        buffer[^1] = 0;

        return (1, buffer);
    }

    private static (int Count, byte[] Data) SerializeStringArray(object value)
    {
        var strings = value switch
        {
            string[] array => array,
            IEnumerable<string> enumerable => enumerable.ToArray(),
            _ => throw new InvalidOperationException("Invalid STRING_ARRAY entry data")
        };

        var encoded = strings
            .Select(s =>
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                var buffer = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
                buffer[^1] = 0;
                return buffer;
            })
            .ToArray();

        var totalLength = encoded.Sum(a => a.Length);
        var result = new byte[totalLength];
        var offset = 0;
        foreach (var array in encoded)
        {
            Buffer.BlockCopy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return (strings.Length, result);
    }

    private static (int Count, byte[] Data) SerializeBin(object value)
    {
        var bytes = value switch
        {
            byte[] array => array,
            IEnumerable<byte> enumerable => enumerable.ToArray(),
            _ => throw new InvalidOperationException("Invalid BIN entry data")
        };

        return (bytes.Length, bytes);
    }

    private static void AlignStream(Stream stream, int alignment)
    {
        if (alignment <= 1)
        {
            return;
        }

        var mod = stream.Position % alignment;
        if (mod == 0)
        {
            return;
        }

        var padding = alignment - mod;
        stream.Write(Zeroes, 0, (int)padding);
    }

    private static int Alignment(HeaderValueType type) => type switch
    {
        HeaderValueType.Int16 => 2,
        HeaderValueType.Int32 => 4,
        HeaderValueType.Int64 => 8,
        _ => 1
    };

    private static byte[] BuildLead(RpmPackage package, string architecture)
    {
        var buffer = new byte[96];
        var span = buffer.AsSpan();

        HeaderLeadMagic.CopyTo(span);
        span[4] = 0x03; // Major
        span[5] = 0x00; // Minor
        BinaryPrimitives.WriteInt16BigEndian(span.Slice(6, 2), 0); // Type
        BinaryPrimitives.WriteInt16BigEndian(span.Slice(8, 2), MapLeadArch(architecture));

        var nameBytes = Encoding.UTF8.GetBytes(package.Metadata.Package);
        var nameLength = Math.Min(nameBytes.Length, 65);
        nameBytes.AsSpan(0, nameLength).CopyTo(span.Slice(10, nameLength));

        BinaryPrimitives.WriteInt16BigEndian(span.Slice(76, 2), 1); // OS (Linux)
        BinaryPrimitives.WriteInt16BigEndian(span.Slice(78, 2), 5); // Signature type

        return buffer;
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

    private static short MapLeadArch(string architecture) => architecture switch
    {
        "noarch" => 0,
        "i386" => 1,
        "x86_64" => 62,
        "aarch64" => 183,
        "armv7hl" => 12,
        _ => 1
    };

    private static string BuildPayloadPath(string normalizedPath, RpmEntryType type)
    {
        var trimmed = normalizedPath.TrimStart('/');
        var path = $"./{trimmed}";

        if (type == RpmEntryType.Directory && !path.EndsWith("/", StringComparison.Ordinal))
        {
            return path;
        }

        return path;
    }

    private static byte[] BuildCpio(IReadOnlyList<FileRecord> files)
    {
        using var stream = new MemoryStream();
        var inode = 1;
        foreach (var file in files)
        {
            WriteCpioEntry(stream, file, inode++);
        }

        WriteCpioTrailer(stream, inode);
        return stream.ToArray();
    }

    private static void WriteCpioEntry(Stream stream, FileRecord file, int inode)
    {
        var nameBytes = Encoding.UTF8.GetBytes(file.PayloadPath);
        var header = new StringBuilder();
        header.Append("070701");
        header.Append(ToHex(inode));
        header.Append(ToHex(file.Mode));
        header.Append(ToHex(file.OwnerId));
        header.Append(ToHex(file.GroupId));
        header.Append(ToHex(1)); // nlink
        header.Append(ToHex(file.MTime));
        header.Append(ToHex(file.Size));
        header.Append(ToHex(0)); // devmajor
        header.Append(ToHex(0)); // devminor
        header.Append(ToHex(0)); // rdevmajor
        header.Append(ToHex(0)); // rdevminor
        header.Append(ToHex(nameBytes.Length + 1));
        header.Append("00000000"); // check

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(nameBytes, 0, nameBytes.Length);
        stream.WriteByte(0);
        PadToFour(stream);

        if (file.Content.Length > 0)
        {
            stream.Write(file.Content, 0, file.Content.Length);
        }

        PadToFour(stream);
    }

    private static void WriteCpioTrailer(Stream stream, int inode)
    {
        var trailer = new FileRecord(
            Index: inode,
            NormalizedPath: "TRAILER!!!",
            Directory: ".",
            BaseName: "TRAILER!!!",
            Mode: FileTypeBits.RegularFile,
            Size: 0,
            OwnerId: 0,
            GroupId: 0,
            OwnerName: "root",
            GroupName: "root",
            Digest: ComputeMd5(Array.Empty<byte>()),
            MTime: 0,
            PayloadPath: "TRAILER!!!",
            Content: Array.Empty<byte>(),
            EntryType: RpmEntryType.File);

        WriteCpioEntry(stream, trailer, inode);
    }

    private static void PadToFour(Stream stream)
    {
        while (stream.Position % 4 != 0)
        {
            stream.WriteByte(0);
        }
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace("\\", "/", StringComparison.Ordinal);
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        var trimmed = normalized.TrimEnd('/');
        return string.IsNullOrEmpty(trimmed) ? "/" : trimmed;
    }

    private static (string Directory, string BaseName) SplitPath(string path)
    {
        var trimmed = path.Trim('/');
        if (string.IsNullOrEmpty(trimmed))
        {
            return ("/", string.Empty);
        }

        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return ("/", trimmed);
        }

        var directory = "/" + trimmed[..lastSlash];
        var baseName = trimmed[(lastSlash + 1)..];
        return (string.IsNullOrEmpty(directory) ? "/" : directory, baseName);
    }

    private static string EnsureTrailingSlash(string path) =>
        path.EndsWith("/", StringComparison.Ordinal) ? path : $"{path}/";

    private static string ComputeMd5(byte[] content)
    {
        var md5 = MD5.HashData(content);
        return Convert.ToHexString(md5).ToLowerInvariant();
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string ToHex(int value) => value.ToString("X8");

    private static readonly byte[] HeaderMagic = { 0x8e, 0xad, 0xe8, 0x01 };
    private static readonly byte[] HeaderLeadMagic = { 0xed, 0xab, 0xee, 0xdb };
    private static readonly byte[] Zeroes = new byte[8];

    private record FileRecord(
        int Index,
        string NormalizedPath,
        string Directory,
        string BaseName,
        int Mode,
        int Size,
        int OwnerId,
        int GroupId,
        string OwnerName,
        string GroupName,
        string Digest,
        int MTime,
        string PayloadPath,
        byte[] Content,
        RpmEntryType EntryType);

    private record HeaderEntry(int Tag, HeaderValueType Type, object Value)
    {
        public static HeaderEntry String(int tag, string value) => new(tag, HeaderValueType.String, value);
        public static HeaderEntry StringArray(int tag, string[] values) => new(tag, HeaderValueType.StringArray, values);
        public static HeaderEntry Bin(int tag, byte[] value) => new(tag, HeaderValueType.Bin, value);
        public static HeaderEntry Int8Array(int tag, byte[] values) => new(tag, HeaderValueType.Int8, values);
        public static HeaderEntry Int32(int tag, int value) => new(tag, HeaderValueType.Int32, new[] { value });
        public static HeaderEntry Int32Array(int tag, int[] values) => new(tag, HeaderValueType.Int32, values);
        public static HeaderEntry Int16Array(int tag, short[] values) => new(tag, HeaderValueType.Int16, values);
        public static HeaderEntry I18NString(int tag, string value) => new(tag, HeaderValueType.I18NString, value);
    }

    private record HeaderIndex(int Tag, HeaderValueType Type, int Offset, int Count);

    private record SerializedEntry(int Offset, int Count);

    private enum HeaderValueType
    {
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

    private static class RpmTags
    {
        public const int HeaderI18NTable = 100;
        public const int Name = 1000;
        public const int Version = 1001;
        public const int Release = 1002;
        public const int Summary = 1004;
        public const int Description = 1005;
        public const int BuildTime = 1006;
        public const int BuildHost = 1007;
        public const int Size = 1009;
        public const int Vendor = 1011;
        public const int License = 1014;
        public const int Packager = 1015;
        public const int Group = 1016;
        public const int Url = 1020;
        public const int Os = 1021;
        public const int Arch = 1022;
        public const int FileSizes = 1028;
        public const int FileStates = 1029;
        public const int FileModes = 1030;
        public const int FileUids = 1031;
        public const int FileGids = 1032;
        public const int FileRDevices = 1033;
        public const int FileMtimes = 1034;
        public const int FileDigests = 1035;
        public const int FileLinkTos = 1036;
        public const int FileFlags = 1037;
        public const int FileUserNames = 1039;
        public const int FileGroupNames = 1040;
        public const int FileVerifyFlags = 1045;
        public const int ArchiveSize = 1046;
        public const int FileDevices = 1095;
        public const int FileInodes = 1096;
        public const int FileLangs = 1097;
        public const int DirIndexes = 1116;
        public const int Basenames = 1117;
        public const int DirNames = 1118;
        public const int PayloadFormat = 1124;
        public const int PayloadCompressor = 1125;
        public const int PayloadFlags = 1126;
        public const int FileDigestAlgorithm = 5011;
    }

    private static class RpmSignatureTags
    {
        public const int Size = 1000;
        public const int Md5 = 1004;
        public const int PayloadSize = 1007;
        public const int Sha256 = 1012;
    }

    [Flags]
    private enum RpmFileFlags
    {
        None = 0,
        Directory = 1 << 4
    }

    [Flags]
    private enum RpmVerifyFlags
    {
        Default = 0
    }

    private enum RpmDigestAlgorithm
    {
        Md5 = 1
    }

    private static class FileTypeBits
    {
        public const int RegularFile = 0x8000;
        public const int Directory = 0x4000;
    }
}
