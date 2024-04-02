using System.Reactive.Linq;
using DotnetPackaging.Common;
using SixLabors.ImageSharp;

namespace DotnetPackaging.Archives.Deb;

public class IconData : IByteFlow
{
    private IconData(int targetSize, byte[] bytes)
    {
        TargetSize = targetSize;
        Bytes = bytes.ToObservable();
        Length = bytes.Length;
    }

    public int TargetSize { get; }

    public IObservable<byte> Bytes { get; }
    public long Length { get; }

    public static async Task<IconData> Create(int targetSize, Image image)
    {
        var bytes = await image.Resize(targetSize, targetSize).ToBytes();
        return new IconData(targetSize, bytes);
    }
}