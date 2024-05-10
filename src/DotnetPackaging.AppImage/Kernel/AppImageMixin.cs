namespace DotnetPackaging.AppImage.Kernel;

public static class AppImageMixin
{
    public static Result<IData> ToData(this AppImage appImage)
    {
        return SquashFS.Create(appImage.Root)
            .Map(data => (IData)new CompositeData(appImage.Runtime, data));
    }
}