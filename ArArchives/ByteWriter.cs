using System.Text;

namespace Archive.Tests;

public class ByteWriter : IByteWriter
{
    private readonly Stream stream;

    public ByteWriter(Stream stream)
    {
        this.stream = stream;
    }

    public void WriteAllBytes(byte[] bytes, string operationName)
    {
        stream.WriteAllBytes(bytes);
    }

    public void WriteString(string str, string operationName, Encoding? encoding = default)
    {
        stream.Write((encoding ?? Encoding.ASCII).GetBytes(str));
    }

    public long Position => stream.Position;
}