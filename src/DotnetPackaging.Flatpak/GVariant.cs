using System.Text;

namespace DotnetPackaging.Flatpak;

/// <summary>
/// GVariant serializer following the GVariant specification.
/// All multi-byte values are stored in big-endian format for OSTree compatibility.
/// </summary>
internal sealed class GVariantBuilder
{
    private readonly MemoryStream _buffer = new();

    public static GVariantBuilder Create() => new();

    /// <summary>
    /// Writes a boolean value (1 byte: 0 or 1).
    /// </summary>
    public GVariantBuilder Bool(bool value)
    {
        _buffer.WriteByte(value ? (byte)1 : (byte)0);
        return this;
    }

    /// <summary>
    /// Writes a single byte.
    /// </summary>
    public GVariantBuilder Byte(byte value)
    {
        _buffer.WriteByte(value);
        return this;
    }

    /// <summary>
    /// Writes a 16-bit signed integer (big-endian).
    /// </summary>
    public GVariantBuilder Int16(short value)
    {
        Align(2);
        WriteBigEndian16((ushort)value);
        return this;
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer (big-endian).
    /// </summary>
    public GVariantBuilder UInt16(ushort value)
    {
        Align(2);
        WriteBigEndian16(value);
        return this;
    }

    /// <summary>
    /// Writes a 32-bit signed integer (big-endian).
    /// </summary>
    public GVariantBuilder Int32(int value)
    {
        Align(4);
        WriteBigEndian32((uint)value);
        return this;
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer (big-endian).
    /// </summary>
    public GVariantBuilder UInt32(uint value)
    {
        Align(4);
        WriteBigEndian32(value);
        return this;
    }

    /// <summary>
    /// Writes a 64-bit signed integer (big-endian).
    /// </summary>
    public GVariantBuilder Int64(long value)
    {
        Align(8);
        WriteBigEndian64((ulong)value);
        return this;
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer (big-endian).
    /// </summary>
    public GVariantBuilder UInt64(ulong value)
    {
        Align(8);
        WriteBigEndian64(value);
        return this;
    }

    /// <summary>
    /// Writes a double-precision floating point (big-endian).
    /// </summary>
    public GVariantBuilder Double(double value)
    {
        Align(8);
        var bits = BitConverter.DoubleToUInt64Bits(value);
        WriteBigEndian64(bits);
        return this;
    }

    /// <summary>
    /// Writes a null-terminated UTF-8 string.
    /// </summary>
    public GVariantBuilder String(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        _buffer.Write(bytes, 0, bytes.Length);
        _buffer.WriteByte(0); // null terminator
        return this;
    }

    /// <summary>
    /// Writes a byte array (ay type).
    /// </summary>
    public GVariantBuilder ByteArray(byte[] data)
    {
        _buffer.Write(data, 0, data.Length);
        return this;
    }

    /// <summary>
    /// Writes a byte array (ay type) from a span.
    /// </summary>
    public GVariantBuilder ByteArray(ReadOnlySpan<byte> data)
    {
        _buffer.Write(data);
        return this;
    }

    /// <summary>
    /// Writes raw bytes without any framing.
    /// </summary>
    public GVariantBuilder Raw(byte[] data)
    {
        _buffer.Write(data, 0, data.Length);
        return this;
    }

    /// <summary>
    /// Writes raw bytes from another builder.
    /// </summary>
    public GVariantBuilder Raw(GVariantBuilder other)
    {
        var data = other.ToArray();
        _buffer.Write(data, 0, data.Length);
        return this;
    }

    /// <summary>
    /// Aligns the buffer to the specified boundary by adding padding bytes.
    /// </summary>
    public GVariantBuilder Align(int alignment)
    {
        var position = (int)_buffer.Position;
        var padding = (alignment - (position % alignment)) % alignment;
        for (var i = 0; i < padding; i++)
        {
            _buffer.WriteByte(0);
        }
        return this;
    }

    /// <summary>
    /// Gets the current position in the buffer.
    /// </summary>
    public int Position => (int)_buffer.Position;

    /// <summary>
    /// Returns the serialized GVariant data as a byte array.
    /// </summary>
    public byte[] ToArray() => _buffer.ToArray();

    private void WriteBigEndian16(ushort value)
    {
        _buffer.WriteByte((byte)(value >> 8));
        _buffer.WriteByte((byte)(value & 0xFF));
    }

    private void WriteBigEndian32(uint value)
    {
        _buffer.WriteByte((byte)(value >> 24));
        _buffer.WriteByte((byte)((value >> 16) & 0xFF));
        _buffer.WriteByte((byte)((value >> 8) & 0xFF));
        _buffer.WriteByte((byte)(value & 0xFF));
    }

    private void WriteBigEndian64(ulong value)
    {
        _buffer.WriteByte((byte)(value >> 56));
        _buffer.WriteByte((byte)((value >> 48) & 0xFF));
        _buffer.WriteByte((byte)((value >> 40) & 0xFF));
        _buffer.WriteByte((byte)((value >> 32) & 0xFF));
        _buffer.WriteByte((byte)((value >> 24) & 0xFF));
        _buffer.WriteByte((byte)((value >> 16) & 0xFF));
        _buffer.WriteByte((byte)((value >> 8) & 0xFF));
        _buffer.WriteByte((byte)(value & 0xFF));
    }
}

/// <summary>
/// Helper for building GVariant tuples with framing offsets.
/// </summary>
internal sealed class GVariantTupleBuilder
{
    private readonly List<byte[]> _elements = new();

    public static GVariantTupleBuilder Create() => new();

    public GVariantTupleBuilder Add(GVariantBuilder element)
    {
        _elements.Add(element.ToArray());
        return this;
    }

    public GVariantTupleBuilder Add(byte[] element)
    {
        _elements.Add(element);
        return this;
    }

    /// <summary>
    /// Builds the tuple with framing offsets for variable-length elements.
    /// OSTree uses simplified framing where each element's end offset is stored.
    /// </summary>
    public byte[] Build()
    {
        if (_elements.Count == 0)
        {
            return Array.Empty<byte>();
        }

        // Calculate total fixed size first
        var totalSize = _elements.Sum(e => e.Length);

        // Determine offset size (1, 2, 4, or 8 bytes based on total size)
        var offsetSize = GetOffsetSize(totalSize + (_elements.Count - 1) * 8); // max estimate

        // Build with offsets
        using var buffer = new MemoryStream();
        var offsets = new List<int>();

        foreach (var element in _elements)
        {
            buffer.Write(element, 0, element.Length);
            offsets.Add((int)buffer.Position);
        }

        // Write framing offsets for all but the last element (last element ends at the framing offsets)
        // Note: OSTree actually needs all offsets including the last, but they're stored at the end
        // The offset size is determined by the total data size
        var dataSize = (int)buffer.Position;
        offsetSize = GetOffsetSize(dataSize + (offsets.Count - 1) * 8);

        for (var i = 0; i < offsets.Count - 1; i++)
        {
            WriteOffset(buffer, offsets[i], offsetSize);
        }

        return buffer.ToArray();
    }

    private static int GetOffsetSize(int totalSize)
    {
        if (totalSize <= 0xFF) return 1;
        if (totalSize <= 0xFFFF) return 2;
        return 4; // int can't exceed 0xFFFFFFFF, so always 4 bytes max
    }

    private static void WriteOffset(MemoryStream buffer, int offset, int offsetSize)
    {
        switch (offsetSize)
        {
            case 1:
                buffer.WriteByte((byte)offset);
                break;
            case 2:
                buffer.WriteByte((byte)(offset >> 8));
                buffer.WriteByte((byte)(offset & 0xFF));
                break;
            case 4:
                buffer.WriteByte((byte)(offset >> 24));
                buffer.WriteByte((byte)((offset >> 16) & 0xFF));
                buffer.WriteByte((byte)((offset >> 8) & 0xFF));
                buffer.WriteByte((byte)(offset & 0xFF));
                break;
            case 8:
                var value = (ulong)offset;
                buffer.WriteByte((byte)(value >> 56));
                buffer.WriteByte((byte)((value >> 48) & 0xFF));
                buffer.WriteByte((byte)((value >> 40) & 0xFF));
                buffer.WriteByte((byte)((value >> 32) & 0xFF));
                buffer.WriteByte((byte)((value >> 24) & 0xFF));
                buffer.WriteByte((byte)((value >> 16) & 0xFF));
                buffer.WriteByte((byte)((value >> 8) & 0xFF));
                buffer.WriteByte((byte)(value & 0xFF));
                break;
        }
    }
}
