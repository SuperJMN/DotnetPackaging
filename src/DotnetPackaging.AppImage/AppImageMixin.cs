using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage;

public static class AppImageMixin
{
    public static Result<IData> ToData(this AppImage appImage)
    {
        return SquashFS.Create(appImage.Root)
            .Map(data => (IData)new CompositeData(appImage.Runtime, data));
    }
}