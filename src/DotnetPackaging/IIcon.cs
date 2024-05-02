using Zafiro.FileSystem;

namespace DotnetPackaging;

public interface IIcon : IObservableDataStream
{
    public int Size { get; }
}