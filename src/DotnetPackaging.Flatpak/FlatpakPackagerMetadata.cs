using DotnetPackaging;

namespace DotnetPackaging.Flatpak;

public sealed class FlatpakPackagerMetadata
{
    public FromDirectoryOptions PackageOptions { get; } = new();
    public FlatpakOptions FlatpakOptions { get; set; } = new();
}
