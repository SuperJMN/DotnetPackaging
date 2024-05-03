using System.Reactive.Linq;
using Zafiro.FileSystem;

namespace DotnetPackaging;

public class ByteArrayData : IData
{
    public ByteArrayData(byte[] content)
    {
        Bytes = Observable.Return(content);
        Length = content.Length;
    }

    public IObservable<byte[]> Bytes { get; }

    public long Length { get; }
}