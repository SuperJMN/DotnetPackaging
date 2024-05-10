using DotnetPackaging.AppImage.Builder;
using DotnetPackaging.AppImage.Kernel;

namespace DotnetPackaging.AppImage;

public class AppImage
{
    public static AppImageBuilder Create() => new(new RuntimeFactory());
}