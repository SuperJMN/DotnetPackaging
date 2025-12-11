namespace DotnetPackaging.Dmg.Hfs.Encoding;

/// <summary>
/// HFS+ Unicode string encoding.
/// Strings are stored as UTF-16 big-endian with Unicode NFD (canonical decomposition).
/// Maximum filename length is 255 UTF-16 code units.
/// </summary>
public static class HfsUnicode
{
    public const int MaxFilenameLength = 255;

    /// <summary>
    /// Encodes a string to HFS+ Unicode format (UTF-16 BE with NFD normalization).
    /// </summary>
    public static byte[] Encode(string name)
    {
        if (string.IsNullOrEmpty(name))
            return Array.Empty<byte>();

        // Apply NFD normalization as per HFS+ spec
        var normalized = name.Normalize(NormalizationForm.FormD);
        
        // Clamp to max length
        if (normalized.Length > MaxFilenameLength)
            normalized = normalized[..MaxFilenameLength];

        // Convert to UTF-16 BE
        var utf16 = System.Text.Encoding.BigEndianUnicode.GetBytes(normalized);
        return utf16;
    }

    /// <summary>
    /// Creates an HFS+ Unicode string with length prefix (UInt16 big-endian).
    /// This is the format used in catalog keys.
    /// </summary>
    public static byte[] EncodeWithLength(string name)
    {
        var encoded = Encode(name);
        var charCount = (ushort)(encoded.Length / 2);
        
        var result = new byte[2 + encoded.Length];
        BigEndianWriter.WriteUInt16(result.AsSpan(0, 2), charCount);
        encoded.CopyTo(result, 2);
        return result;
    }

    /// <summary>
    /// Writes an HFS+ Unicode string with length prefix to a span.
    /// Returns the number of bytes written.
    /// </summary>
    public static int WriteWithLength(Span<byte> destination, string name)
    {
        var encoded = Encode(name);
        var charCount = (ushort)(encoded.Length / 2);
        
        BigEndianWriter.WriteUInt16(destination[..2], charCount);
        encoded.CopyTo(destination[2..]);
        return 2 + encoded.Length;
    }

    /// <summary>
    /// Decodes an HFS+ Unicode string from UTF-16 BE bytes.
    /// </summary>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        return System.Text.Encoding.BigEndianUnicode.GetString(bytes);
    }

    /// <summary>
    /// Compares two HFS+ Unicode strings using case-insensitive comparison.
    /// HFS+ uses case-insensitive but case-preserving comparisons by default.
    /// </summary>
    public static int Compare(string a, string b)
    {
        var normalizedA = a.Normalize(NormalizationForm.FormD);
        var normalizedB = b.Normalize(NormalizationForm.FormD);
        return string.Compare(normalizedA, normalizedB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the byte length of an encoded HFS+ Unicode string (without length prefix).
    /// </summary>
    public static int GetByteLength(string name)
        => Encode(name).Length;

    /// <summary>
    /// Gets the byte length of an encoded HFS+ Unicode string with length prefix.
    /// </summary>
    public static int GetByteLengthWithPrefix(string name)
        => 2 + GetByteLength(name);
}
