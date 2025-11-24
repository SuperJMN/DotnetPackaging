using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Bytes;

public interface IData : IByteSource
{
    long Length { get; }
}
