namespace DotnetPackaging.Flatpak;

// Minimal GVariant builder (scaffold). Not spec-complete; replace incrementally.
internal sealed class GVariant
{
    private readonly List<byte> buffer = new();

    public static GVariant Create() => new();

    public GVariant String(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        buffer.AddRange(bytes);
        buffer.Add(0); // nul-terminated (scaffold)
        return this;
    }

    public GVariant UInt64(ulong value)
    {
        buffer.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    public GVariant Bytes(byte[] data)
    {
        buffer.AddRange(data);
        return this;
    }

    public byte[] ToArray() => buffer.ToArray();
}
