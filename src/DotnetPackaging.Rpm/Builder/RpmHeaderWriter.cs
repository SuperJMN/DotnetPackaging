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
            // NOTE: FileStates (1029) is NOT included - it's an internal RPM database tag populated during installation

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

        return RpmHeaderBuilder.BuildWithRegion(headerEntries, RpmTag.HeaderImmutable);
    }

    public static byte[] BuildSignatureHeader(byte[] header, byte[] payload)
    {
        var headerPayload = Combine(header, payload);
        var size = headerPayload.Length;

        // Calculate checksums - MD5 is REQUIRED per RPM v4 specification
        // https://rpm-software-management.github.io/rpm/manual/format_v4.html
        // "All packages carry at least HEADERSIGNATURES, (LONG)SIZE, MD5 and SHA1"
        var md5 = MD5.HashData(headerPayload);
        var sha1Header = SHA1.HashData(header);
        var sha256Header = SHA256.HashData(header);

        // Convert to hex strings (lowercase as per RPM convention)
        var sha1String = Convert.ToHexString(sha1Header).ToLowerInvariant();
        var sha256String = Convert.ToHexString(sha256Header).ToLowerInvariant();

        // Build signature header with region tag 62
        // Required tags per spec: HEADERSIGNATURES (62), SIZE (257), MD5 (261), SHA1 (269), SHA256 (273)
        var entries = new List<RpmHeaderEntry>
        {
            RpmHeaderEntry.Int32(RpmSignatureTag.Size, size),
            RpmHeaderEntry.Bin(RpmSignatureTag.Md5, md5),
            RpmHeaderEntry.String(RpmSignatureTag.Sha1Header, sha1String),
            RpmHeaderEntry.String(RpmSignatureTag.Sha256Header, sha256String)
        };

        return RpmHeaderBuilder.BuildWithRegion(entries, RpmSignatureTag.Reserved);
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
            var buffer = new byte[4];
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
            var buffer = new byte[2];
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
        var ordered = entries.OrderBy(entry => entry.Tag).ToList();
        
        // Handle Immutable Region (Tags 62 or 63)
        var immutableIndex = ordered.FindIndex(e => e.Tag is 62 or 63);
        if (immutableIndex != -1)
        {
            var immutableEntry = ordered[immutableIndex];
            // The data for the immutable region is a 16-byte entry representing the region itself.
            // The Count in the main index is 16 (the size of the data).
            ordered[immutableIndex] = immutableEntry with { Count = 16, Data = new byte[16] };
        }

        using var data = new MemoryStream();
        var indexEntries = new List<RpmHeaderIndexEntry>(ordered.Count);

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

        // Now that we have the index entries, we must update the Immutable Region data if it exists.
        if (immutableIndex != -1)
        {
            var ie = indexEntries[immutableIndex];
            
            // 1. Prepare Header Data (16 bytes)
            var headData = new byte[16];
            BinaryPrimitives.WriteInt32BigEndian(headData.AsSpan(0, 4), ie.Tag);
            BinaryPrimitives.WriteInt32BigEndian(headData.AsSpan(4, 4), (int)ie.Type);
            BinaryPrimitives.WriteInt32BigEndian(headData.AsSpan(8, 4), -(ordered.Count * 16));
            BinaryPrimitives.WriteInt32BigEndian(headData.AsSpan(12, 4), ordered.Count);
            
            // 2. Prepare Trailer Data (16 bytes)
            var trailData = new byte[16];
            BinaryPrimitives.WriteInt32BigEndian(trailData.AsSpan(0, 4), ie.Tag);
            BinaryPrimitives.WriteInt32BigEndian(trailData.AsSpan(4, 4), (int)ie.Type);
            BinaryPrimitives.WriteInt32BigEndian(trailData.AsSpan(8, 4), -(ordered.Count * 16));
            BinaryPrimitives.WriteInt32BigEndian(trailData.AsSpan(12, 4), 0); // Count is 0 for trailer
            
            // Re-write the data stream to include the correct header data and append the trailer
            var currentData = data.ToArray();
            
            // Copy headData to its assigned offset
            Buffer.BlockCopy(headData, 0, currentData, ie.Offset, 16);
            
            // Create final data stream (currentData + trailData)
            var finalData = new byte[currentData.Length + 16];
            Buffer.BlockCopy(currentData, 0, finalData, 0, currentData.Length);
            Buffer.BlockCopy(trailData, 0, finalData, currentData.Length, 16);
            
            using var header = new MemoryStream();
            header.Write(HeaderMagic, 0, HeaderMagic.Length);
            header.Write(new byte[4], 0, 4);
            WriteInt32(header, ordered.Count);
            WriteInt32(header, finalData.Length);

            for (int i = 0; i < indexEntries.Count; i++)
            {
                var indexEntry = indexEntries[i];
                if (i == immutableIndex)
                {
                    // Special case for Region Tag: Offset points to the TRAILER
                    var trailerOffset = currentData.Length;
                    WriteInt32(header, indexEntry.Tag);
                    WriteInt32(header, (int)indexEntry.Type);
                    WriteInt32(header, trailerOffset);
                    WriteInt32(header, indexEntry.Count);
                }
                else
                {
                    WriteInt32(header, indexEntry.Tag);
                    WriteInt32(header, (int)indexEntry.Type);
                    WriteInt32(header, indexEntry.Offset);
                    WriteInt32(header, indexEntry.Count);
                }
            }

            header.Write(finalData, 0, finalData.Length);
            return header.ToArray();
        }

        using var finalHeader = new MemoryStream();
        finalHeader.Write(HeaderMagic, 0, HeaderMagic.Length);
        finalHeader.Write(new byte[4], 0, 4);
        WriteInt32(finalHeader, ordered.Count);
        WriteInt32(finalHeader, (int)data.Length);

        foreach (var indexEntry in indexEntries)
        {
            WriteInt32(finalHeader, indexEntry.Tag);
            WriteInt32(finalHeader, (int)indexEntry.Type);
            WriteInt32(finalHeader, indexEntry.Offset);
            WriteInt32(finalHeader, indexEntry.Count);
        }

        var finalDataBytes = data.ToArray();
        finalHeader.Write(finalDataBytes, 0, finalDataBytes.Length);
        return finalHeader.ToArray();
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
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    /// <summary>
    /// Builds a header with a region tag (62 for signatures, 63 for metadata).
    /// The region tag has NO data in the normal data section.
    /// The region tag offset points to a 16-byte trailer APPENDED at the end of the data section.
    /// </summary>
    public static byte[] BuildWithRegion(IReadOnlyCollection<RpmHeaderEntry> entries, int regionTag)
    {
        // Build the data section from regular entries FIRST (no placeholder)
        var ordered = entries.OrderBy(entry => entry.Tag).ToList();
        
        using var data = new MemoryStream();
        var indexEntries = new List<RpmHeaderIndexEntry>(ordered.Count + 1); // +1 for region tag

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

        // The trailer will be appended at the end of data
        var trailerOffset = (int)data.Length;
        
        // Total number of entries = regular entries + 1 region tag
        var totalEntries = ordered.Count + 1;
        
        // Create the region trailer (16 bytes) - this goes at the end of data section
        // The trailer looks like an index entry: tag, type, negative_offset, count
        // negative_offset = -(total_entries * 16) points back to start of index
        var trailerData = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(trailerData.AsSpan(0, 4), regionTag);
        BinaryPrimitives.WriteInt32BigEndian(trailerData.AsSpan(4, 4), (int)RpmTagType.Bin);
        BinaryPrimitives.WriteInt32BigEndian(trailerData.AsSpan(8, 4), -(totalEntries * 16));
        BinaryPrimitives.WriteInt32BigEndian(trailerData.AsSpan(12, 4), 16); // Count = 16 (size of trailer)

        // Append trailer to data
        data.Write(trailerData, 0, 16);
        
        var finalData = data.ToArray();
        var finalDataLength = finalData.Length;

        // Build the header structure
        using var header = new MemoryStream();
        header.Write(HeaderMagic, 0, HeaderMagic.Length);
        header.Write(new byte[4], 0, 4); // Reserved
        WriteInt32(header, totalEntries);
        WriteInt32(header, finalDataLength);

        // Write region tag index entry FIRST (lowest tag number)
        // It points to the trailer at the end of data section
        WriteInt32(header, regionTag);
        WriteInt32(header, (int)RpmTagType.Bin);
        WriteInt32(header, trailerOffset);
        WriteInt32(header, 16); // Count = 16 (size of trailer)

        // Write remaining index entries
        foreach (var ie in indexEntries)
        {
            WriteInt32(header, ie.Tag);
            WriteInt32(header, (int)ie.Type);
            WriteInt32(header, ie.Offset);
            WriteInt32(header, ie.Count);
        }

        header.Write(finalData, 0, finalData.Length);
        return header.ToArray();
    }
}

internal readonly record struct RpmHeaderIndexEntry(int Tag, RpmTagType Type, int Offset, int Count);

internal static class RpmTag
{
    public const int HeaderI18nTable = 100;
    public const int HeaderImmutable = 63;
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
    public const int Reserved = 62;
    public const int Md5 = 261;
    public const int Sha1Header = 269;
    public const int Sha256Header = 273;
}
