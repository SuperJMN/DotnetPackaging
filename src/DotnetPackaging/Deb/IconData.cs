using System.Reactive.Linq;
using DotnetPackaging.Common;
using static Zafiro.Mixins.ObservableEx;

namespace DotnetPackaging.Deb;

public class IconData
{
    public IconData(int targetSize, IByteStore sourceBytes)
    {
        TargetSize = targetSize;
        SourceBytes = sourceBytes;
    }

    public int TargetSize { get; }

    public IByteStore SourceBytes { get; }

    public IByteStore TargetedBytes
    {
        get
        {
            var image = Image.Load(SourceBytes.ToEnumerable().ToArray());
            image.Resize(TargetSize, TargetSize);
            var memoryStream = new MemoryStream();
            image.SaveAsPng(memoryStream);
            return new ByteStore(memoryStream.ToArray().ToObservable(), memoryStream.Length);
        }
    }

    //private Task<Image> Image()
    //{
    //    var bytes = SourceBytes.ToEnumerable().ToArray();
    //    return SixLabors.ImageSharp.Image.LoadAsync(new MemoryStream(bytes));
    //}
}