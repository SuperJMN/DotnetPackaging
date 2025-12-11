namespace DotnetPackaging.Dmg.Hfs.Encoding;

/// <summary>
/// Functional big-endian binary writer for HFS+ structures.
/// All HFS+ on-disk structures use big-endian byte order.
/// </summary>
public static class BigEndianWriter
{
    public static byte[] WriteUInt16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] WriteInt16(short value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] WriteUInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] WriteInt32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] WriteUInt64(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] WriteInt64(long value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    public static void WriteUInt16(Span<byte> destination, ushort value)
        => BinaryPrimitives.WriteUInt16BigEndian(destination, value);

    public static void WriteInt16(Span<byte> destination, short value)
        => BinaryPrimitives.WriteInt16BigEndian(destination, value);

    public static void WriteUInt32(Span<byte> destination, uint value)
        => BinaryPrimitives.WriteUInt32BigEndian(destination, value);

    public static void WriteInt32(Span<byte> destination, int value)
        => BinaryPrimitives.WriteInt32BigEndian(destination, value);

    public static void WriteUInt64(Span<byte> destination, ulong value)
        => BinaryPrimitives.WriteUInt64BigEndian(destination, value);

    public static void WriteInt64(Span<byte> destination, long value)
        => BinaryPrimitives.WriteInt64BigEndian(destination, value);

    /// <summary>
    /// Writes a 4-character ASCII signature (e.g., "H+", "koly").
    /// </summary>
    public static byte[] WriteSignature(string signature)
    {
        if (signature.Length > 4)
            throw new ArgumentException("Signature must be at most 4 characters", nameof(signature));
        
        var bytes = new byte[4];
        var ascii = System.Text.Encoding.ASCII.GetBytes(signature.PadRight(4, '\0'));
        ascii.CopyTo(bytes, 0);
        return bytes;
    }

    public static void WriteSignature(Span<byte> destination, string signature)
    {
        if (signature.Length > 4)
            throw new ArgumentException("Signature must be at most 4 characters", nameof(signature));
        
        var ascii = System.Text.Encoding.ASCII.GetBytes(signature.PadRight(4, '\0'));
        ascii.CopyTo(destination);
    }
}
