using System.Reactive.Linq;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging;

public class ByteArrayByteProvider : IByteProvider
{
    public ByteArrayByteProvider(byte[] content)
    {
        Bytes = Observable.Return(content);
        Length = content.Length;
    }

    public IObservable<byte[]> Bytes { get; }

    public long Length { get; }
}