using System.Reactive.Linq;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb;

public class CompositeByteProvider : IByteProvider
{
    public CompositeByteProvider(params IByteProvider[] byteProviders)
    {
        Bytes = byteProviders.Select(x => x.Bytes).Concat();
        Length = byteProviders.Sum(x => x.Length);
    }

    public IObservable<byte[]> Bytes { get; }
    public long Length { get; }
}