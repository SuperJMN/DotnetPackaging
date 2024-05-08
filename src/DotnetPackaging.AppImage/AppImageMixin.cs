using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Tests;

public static class AppImageMixin
{
    public static IData ToData(this AppImage appImage)
    {
        return new CompositeData(appImage.Runtime);
    }
}