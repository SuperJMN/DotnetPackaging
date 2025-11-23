using Zafiro.DivineBytes;

namespace DotnetPackaging;

public interface IIcon : IByteSource
{
    public int Size { get; }
}
