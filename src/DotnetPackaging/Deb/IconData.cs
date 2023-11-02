using System.Reactive.Linq;
using Zafiro.IO;

namespace DotnetPackaging.Deb;

public class IconData
{
    private readonly int targetSize;

    public IconData(int targetSize, Func<IObservable<byte>> source)
    {
        this.targetSize = targetSize;
        Source = source;
    }

    public Func<IObservable<byte>> Source { get; }

    public Func<IObservable<byte>> Bytes
    {
        get
        {
            return () =>
            {
                var bytesFromResize = Zafiro.Mixins.ObservableEx.Using(LoadImage, image =>
                {
                    return Zafiro.Mixins.ObservableEx.Using(async () =>
                    {
                        image.Mutate(context => context.Resize(targetSize, targetSize));
                        var output = new MemoryStream();
                        await image.SaveAsPngAsync(output);
                        output.Position = 0;
                        return output;
                    }, stream => stream.ToObservable());
                });

                return bytesFromResize;
            };
        }
    }

    private async Task<Image> LoadImage()
    {
        await using var memoryStream = new MemoryStream();
        await Source().DumpTo(memoryStream);
        memoryStream.Position = 0;
        return await Image.LoadAsync(memoryStream);
    }
}