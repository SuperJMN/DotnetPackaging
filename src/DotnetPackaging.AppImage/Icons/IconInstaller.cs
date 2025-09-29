using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Icons;

public static class IconInstaller
{
    // Placeholder discovery: return no icons so fallback path in AppImageFactory executes
    public static IReadOnlyDictionary<string, IByteSource> Discover(
        IContainer applicationRoot,
        Metadata.AppImageMetadata metadata,
        string iconName)
    {
        return new Dictionary<string, IByteSource>();
    }
}
