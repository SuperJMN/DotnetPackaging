using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Archives.Deb;

public class IconData : IByteFlow
{
    private readonly IObservable<byte> imageBytesObs;

    public IconData(int targetSize, Image image)
    {
        TargetSize = targetSize;

        imageBytesObs = Observable.FromAsync(() => image.Resize(targetSize, TargetSize).ToBytes())
            .SelectMany(b => b)
            .Replay()
            .RefCount();
    }

    public int TargetSize { get; }
    public IObservable<byte> Bytes => imageBytesObs;
    public long Length
    {
        get
        {
            var enumerable = imageBytesObs.ToEnumerable();
            return enumerable.Count();
        }
    }
}