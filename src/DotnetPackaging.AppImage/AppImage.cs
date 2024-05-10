using DotnetPackaging.AppImage.Builder;

namespace DotnetPackaging.AppImage;

public class AppImage
{
    public static AppImageBuilder Create() => new(new RuntimeFactory());
}