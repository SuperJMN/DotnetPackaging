using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging;

public interface IIcon : IByteProvider
{
    public int Size { get; }
}