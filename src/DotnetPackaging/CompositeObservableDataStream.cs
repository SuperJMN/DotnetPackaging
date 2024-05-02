using System.Reactive.Linq;
using Zafiro.FileSystem;

namespace DotnetPackaging;

public class CompositeObservableDataStream : IObservableDataStream
{
    public CompositeObservableDataStream(params IObservableDataStream[] byteProviders)
    {
        Bytes = byteProviders.Select(x => x.Bytes).Concat();
        Length = byteProviders.Sum(x => x.Length);
    }

    public IObservable<byte[]> Bytes { get; }
    public long Length { get; }
}