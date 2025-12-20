using DotnetPackaging;

namespace DotnetPackaging.AppImage;

public sealed class AppImagePackagerMetadata
{
    public FromDirectoryOptions PackageOptions { get; } = new();
    public AppImageOptions AppImageOptions { get; } = new();
}
