namespace DotnetPackaging.Deb.Archives.Tar;

public static class ByteProviderMixin
{
    public static IData PadToNearestMultiple(this IData data, int multiple)
    {
        return new CompositeData(data, new PaddingProvider(0, (int)(data.Length.RoundUpToNearestMultiple(multiple) - data.Length)));
    }
}