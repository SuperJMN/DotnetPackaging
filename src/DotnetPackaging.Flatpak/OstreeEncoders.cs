using System.Security.Cryptography;
using System.Text;

namespace DotnetPackaging.Flatpak;

/// <summary>
/// OSTree object encoders following the OSTree specification.
/// All objects use GVariant serialization in big-endian format.
/// </summary>
internal static class OstreeEncoders
{
    /// <summary>
    /// Encodes an OSTree commit object.
    /// Format: (a{sv}aya(say)sstayay)
    /// - a{sv}: metadata dictionary
    /// - ay: parent commit checksum (empty for root)
    /// - a(say): related objects
    /// - s: subject
    /// - s: body
    /// - t: timestamp (big-endian uint64, seconds since epoch)
    /// - ay: root dirtree contents checksum (32 bytes)
    /// - ay: root dirmeta checksum (32 bytes)
    /// </summary>
    public static byte[] EncodeCommit(
        byte[] treeContentsChecksum,
        byte[] dirmetaChecksum,
        string subject,
        string body,
        DateTimeOffset timestamp,
        byte[]? parentChecksum = null,
        Dictionary<string, byte[]>? metadata = null)
    {
        var builder = GVariantBuilder.Create();

        // a{sv}: metadata dictionary (simplified: write count + entries)
        // For empty dict, just write empty array marker
        if (metadata == null || metadata.Count == 0)
        {
            // Empty array - just zero bytes for the array portion
        }
        else
        {
            foreach (var kv in metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                builder.String(kv.Key);
                builder.ByteArray(kv.Value);
            }
        }

        // Framing offset for metadata end
        var metadataEnd = builder.Position;

        // ay: parent checksum (empty array if null)
        if (parentChecksum != null && parentChecksum.Length > 0)
        {
            builder.ByteArray(parentChecksum);
        }
        var parentEnd = builder.Position;

        // a(say): related objects (empty for now)
        var relatedEnd = builder.Position;

        // s: subject
        builder.String(subject);
        var subjectEnd = builder.Position;

        // s: body
        builder.String(body);
        var bodyEnd = builder.Position;

        // t: timestamp (uint64 big-endian)
        builder.UInt64((ulong)timestamp.ToUnixTimeSeconds());
        var timestampEnd = builder.Position;

        // ay: root dirtree contents checksum (32 bytes SHA256)
        builder.ByteArray(treeContentsChecksum);
        var treeEnd = builder.Position;

        // ay: root dirmeta checksum (32 bytes SHA256)
        builder.ByteArray(dirmetaChecksum);

        // Write framing offsets at the end (for variable-length elements)
        // Offsets are for: metadata, parent, related, subject, body
        // Fixed-size elements (timestamp, checksums) don't need offsets
        var offsets = new[] { metadataEnd, parentEnd, relatedEnd, subjectEnd, bodyEnd };
        var totalSize = builder.Position + offsets.Length; // approximate
        var offsetSize = totalSize <= 255 ? 1 : (totalSize <= 65535 ? 2 : 4);

        foreach (var offset in offsets)
        {
            WriteOffset(builder, offset, offsetSize);
        }

        return builder.ToArray();
    }

    /// <summary>
    /// Encodes an OSTree dirtree object.
    /// Format: (a(say)a(sayay))
    /// - a(say): files array [(filename, checksum)]
    /// - a(sayay): directories array [(dirname, contents_checksum, meta_checksum)]
    /// </summary>
    public static byte[] EncodeDirTree(
        IEnumerable<(string Name, byte[] Checksum)> files,
        IEnumerable<(string Name, byte[] ContentsChecksum, byte[] MetaChecksum)> directories)
    {
        var builder = GVariantBuilder.Create();

        // Files array: a(say)
        var sortedFiles = files.OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
        foreach (var file in sortedFiles)
        {
            builder.String(file.Name);
            builder.ByteArray(file.Checksum);
        }
        var filesEnd = builder.Position;

        // Directories array: a(sayay)
        var sortedDirs = directories.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        foreach (var dir in sortedDirs)
        {
            builder.String(dir.Name);
            builder.ByteArray(dir.ContentsChecksum);
            builder.ByteArray(dir.MetaChecksum);
        }

        // Framing offset for files array end
        var totalSize = builder.Position + 4;
        var offsetSize = totalSize <= 255 ? 1 : (totalSize <= 65535 ? 2 : 4);
        WriteOffset(builder, filesEnd, offsetSize);

        return builder.ToArray();
    }

    /// <summary>
    /// Encodes an OSTree dirmeta object.
    /// Format: (uuuay)
    /// - u: uid (uint32)
    /// - u: gid (uint32)
    /// - u: mode (uint32)
    /// - ay: extended attributes (usually empty)
    /// </summary>
    public static byte[] EncodeDirMeta(uint uid, uint gid, uint mode, byte[]? xattrs = null)
    {
        var builder = GVariantBuilder.Create();

        builder.UInt32(uid);
        builder.UInt32(gid);
        builder.UInt32(mode);
        builder.ByteArray(xattrs ?? Array.Empty<byte>());

        return builder.ToArray();
    }

    /// <summary>
    /// Encodes an OSTree file content object.
    /// For regular files, this is just the raw file content.
    /// For executables, mode is stored separately in dirmeta.
    /// </summary>
    public static byte[] EncodeFileContent(byte[] content)
    {
        return content;
    }

    /// <summary>
    /// Computes SHA256 checksum of data (used for OSTree object addressing).
    /// </summary>
    public static byte[] ComputeChecksum(byte[] data)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }

    /// <summary>
    /// Converts checksum bytes to hex string.
    /// </summary>
    public static string ChecksumToHex(byte[] checksum)
    {
        return Convert.ToHexString(checksum).ToLowerInvariant();
    }

    /// <summary>
    /// Converts hex string to checksum bytes.
    /// </summary>
    public static byte[] HexToChecksum(string hex)
    {
        return Convert.FromHexString(hex);
    }

    private static void WriteOffset(GVariantBuilder builder, int offset, int offsetSize)
    {
        switch (offsetSize)
        {
            case 1:
                builder.Byte((byte)offset);
                break;
            case 2:
                builder.UInt16((ushort)offset);
                break;
            case 4:
                builder.UInt32((uint)offset);
                break;
        }
    }
}
