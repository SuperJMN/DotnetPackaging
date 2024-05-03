using Zafiro.FileSystem;

namespace DotnetPackaging;

public interface IIcon : IData
{
    public int Size { get; }
}