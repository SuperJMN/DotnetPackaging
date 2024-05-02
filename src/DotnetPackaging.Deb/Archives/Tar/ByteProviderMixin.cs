using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class ByteProviderMixin
{
    public static IObservableDataStream PadToNearestMultiple(this IObservableDataStream observableDataStream, int multiple)
    {
        return new CompositeObservableDataStream(observableDataStream, new PaddingProvider(0, (int)(observableDataStream.Length.RoundUpToNearestMultiple(multiple) - observableDataStream.Length)));
    }
}