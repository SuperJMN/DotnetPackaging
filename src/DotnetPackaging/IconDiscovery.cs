using Zafiro.DivineBytes;
using DivinePath = Zafiro.DivineBytes.Path;

namespace DotnetPackaging;

public static class IconDiscovery
{
    public static IconDiscoveryResult Discover(IContainer container, string iconName)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (string.IsNullOrWhiteSpace(iconName))
        {
            throw new ArgumentException("Icon name cannot be null or whitespace", nameof(iconName));
        }

        var normalizedIconName = iconName.Trim();

        var rootResources = container.Resources
            .Select(resource => (INamedByteSourceWithPath)new ResourceWithPath(DivinePath.Empty, resource))
            .ToList();

        var svgResource = rootResources
            .FirstOrDefault(resource => IsSvgCandidate(resource.Name, normalizedIconName));

        var pngResource = PickPngCandidate(rootResources, normalizedIconName);

        var iconFiles = new Dictionary<string, IByteSource>(StringComparer.Ordinal);

        if (svgResource != null)
        {
            iconFiles[$"usr/share/icons/hicolor/scalable/apps/{normalizedIconName}.svg"] = svgResource;
        }

        if (pngResource != null)
        {
            iconFiles[$"usr/share/icons/hicolor/256x256/apps/{normalizedIconName}.png"] = pngResource;
        }

        var dirIcon = pngResource is null ? Maybe<IByteSource>.None : Maybe<IByteSource>.From(pngResource);

        return new IconDiscoveryResult(
            normalizedIconName,
            svgResource is null ? Maybe<INamedByteSourceWithPath>.None : Maybe<INamedByteSourceWithPath>.From(svgResource),
            pngResource is null ? Maybe<INamedByteSourceWithPath>.None : Maybe<INamedByteSourceWithPath>.From(pngResource),
            iconFiles,
            dirIcon);
    }

    private static INamedByteSourceWithPath? PickPngCandidate(IEnumerable<INamedByteSourceWithPath> resources, string iconName)
    {
        static bool Equals(string candidate, string expected) => candidate.Equals(expected, StringComparison.OrdinalIgnoreCase);

        var exactCandidates = new[]
        {
            "icon-512.png",
            "icon-256.png",
            "icon.png",
            $"{iconName}-512.png",
            $"{iconName}-256.png",
            $"{iconName}.png"
        };

        foreach (var candidate in exactCandidates)
        {
            var exactMatch = resources.FirstOrDefault(resource => Equals(resource.Name, candidate));
            if (exactMatch != null)
            {
                return exactMatch;
            }
        }

        return resources
            .FirstOrDefault(resource => resource.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSvgCandidate(string resourceName, string iconName)
    {
        return resourceName.Equals("icon.svg", StringComparison.OrdinalIgnoreCase)
               || resourceName.EndsWith("-icon.svg", StringComparison.OrdinalIgnoreCase)
               || resourceName.Equals($"{iconName}.svg", StringComparison.OrdinalIgnoreCase)
               || resourceName.Equals($"{iconName}-icon.svg", StringComparison.OrdinalIgnoreCase);
    }
}

public record IconDiscoveryResult(
    string IconName,
    Maybe<INamedByteSourceWithPath> Svg,
    Maybe<INamedByteSourceWithPath> Png,
    IReadOnlyDictionary<string, IByteSource> IconFiles,
    Maybe<IByteSource> DirIcon);
