using System.Reactive.Linq;
using Zafiro.FileSystem;

namespace DotnetPackaging;

public class ByteArrayObservableDataStream : IObservableDataStream
{
    public ByteArrayObservableDataStream(byte[] content)
    {
        Bytes = Observable.Return(content);
        Length = content.Length;
    }

    public IObservable<byte[]> Bytes { get; }

    public long Length { get; }
}