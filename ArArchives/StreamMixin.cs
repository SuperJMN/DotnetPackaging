namespace Archive.Tests;

public static class StreamMixin
{
    public static void WriteAllBytes(this Stream stream, byte[] data)
    {
        stream.Write(data, 0, data.Length);
    }
}