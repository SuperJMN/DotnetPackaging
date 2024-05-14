using DotnetPackaging.AppImage.Builder;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.AppImage;

public class AppImage
{
    public static AppImageBuilder From() => new(new RuntimeFactory());
}