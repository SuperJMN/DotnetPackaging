using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Archives.Deb;

public class IconData : IByteFlow
{
    public IconData(int targetSize, Image image)
    {
        TargetSize = targetSize;

        var imageObs = Observable
            .FromAsync(() => image.Resize(targetSize, TargetSize).ToBytes())
            .SelectMany(bytes => bytes.ToObservable());

        Bytes = imageObs;
        Length = imageObs.ToEnumerable().Count();
    }

    public int TargetSize { get; }
    public IObservable<byte> Bytes { get; }
    public long Length { get; }
}