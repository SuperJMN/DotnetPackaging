using System.Reactive.Linq;

namespace DotnetPackaging.Common;

public interface IByteStore : IObservable<byte>
{
    long Length { get; }
}

public class ByteStore : IByteStore
{
    public long Length { get; }
    private readonly IObservable<byte> inner;

    public ByteStore(IObservable<byte> inner, long size)
    {
        Length = size;
        this.inner = inner;
    }

    public IDisposable Subscribe(IObserver<byte> observer) => inner.Subscribe(observer);

    public static ByteStore Empty = new ByteStore(Observable.Empty<byte>(), 0);
}