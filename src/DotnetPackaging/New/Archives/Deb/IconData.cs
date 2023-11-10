using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.New.Archives.Deb;

public class IconData : IByteFlow
{
    public IconData(int targetSize, Image image)
    {
        TargetSize = targetSize;

        var imageObs = Observable.Defer(() => image.Resize(targetSize, TargetSize).EncodeAsObservable())
            .Publish()
            .RefCount();

        Bytes = imageObs;
        Length = imageObs.ToEnumerable().Count();
    }

    public int TargetSize { get; }
    public IObservable<byte> Bytes { get; }
    public long Length { get; }
}