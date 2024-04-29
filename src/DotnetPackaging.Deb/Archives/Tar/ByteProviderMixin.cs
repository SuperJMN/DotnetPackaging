using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class ByteProviderMixin
{
    public static IByteProvider PadToNearestMultiple(this IByteProvider byteProvider, int multiple)
    {
        return new CompositeByteProvider(byteProvider, new PaddingProvider(0, (int)(byteProvider.Length.RoundUpToNearestMultiple(multiple) - byteProvider.Length)));
    }
}