using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Archives.Deb;

public class IconData : IByteFlow
{
    private readonly IObservable<byte> imageObs;

    public IconData(int targetSize, Image image)
    {
        TargetSize = targetSize;

        imageObs = Observable
            .FromAsync(() => image.Resize(targetSize, TargetSize).ToBytes())
            .SelectMany(bytes => bytes.ToObservable())
            .FirstAsync();
    }

    public int TargetSize { get; }
    public IObservable<byte> Bytes => imageObs;
    public long Length => imageObs.ToEnumerable().Count();
}