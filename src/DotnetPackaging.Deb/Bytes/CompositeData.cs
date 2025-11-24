using System.Reactive.Linq;

namespace DotnetPackaging.Deb.Bytes;

public class CompositeData : IData
{
    private readonly IReadOnlyCollection<IData> segments;

    public CompositeData(params IData[] segments)
    {
        this.segments = segments;
    }

    public IObservable<byte[]> Bytes => segments.ToObservable().SelectMany(segment => segment.Bytes);

    public long Length => segments.Sum(segment => segment.Length);

    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return Bytes.Subscribe(observer);
    }
}
