using System.Reactive.Linq;
using static Zafiro.Mixins.ObservableEx;

namespace DotnetPackaging.Deb;

public class IconData
{
    public IconData(int targetSize, Func<IObservable<byte>> sourceBytes)
    {
        TargetSize = targetSize;
        SourceBytes = sourceBytes;
    }

    public int TargetSize { get; }

    public Func<IObservable<byte>> SourceBytes { get; }

    public Func<IObservable<byte>> IconBytes => () => { return Using(Image, image => Observable.Using(() => image.Resize(TargetSize, TargetSize), resizedImage => resizedImage.EncodeAsObservable())); };

    private Task<Image> Image()
    {
        var bytes = SourceBytes().ToEnumerable().ToArray();
        return SixLabors.ImageSharp.Image.LoadAsync(new MemoryStream(bytes));
    }
}