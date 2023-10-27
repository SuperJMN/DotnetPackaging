using System.Text;

namespace Archive.Tests;

public interface IByteWriter
{
    void WriteAllBytes(byte[] bytes, string fileMode);
    void WriteString(string str, string operationName, Encoding? encoding = default);
    long Position { get; }
}