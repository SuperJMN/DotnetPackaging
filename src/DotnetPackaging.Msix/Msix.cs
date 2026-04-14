using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core;
using DotnetPackaging.Msix.Core.Assets;
using DotnetPackaging.Msix.Core.Manifest;
using DotnetPackaging.Msix.Core.Signing;
using File = System.IO.File;

namespace DotnetPackaging.Msix;

internal class Msix
{
    public static Result<IByteSource> FromDirectory(IContainer container, Maybe<X509Certificate2> certificate, Maybe<ILogger> logger)
    {
        return new Core.MsixPackager(certificate, logger).Pack(container);
    }

    public static Result<IByteSource> FromDirectoryAndMetadata(
        IContainer container,
        AppManifestMetadata metadata,
        Maybe<byte[]> sourceIcon,
        Maybe<X509Certificate2> certificate,
        Maybe<ILogger> logger)
    {
        var generateAppManifest = AppManifestGenerator.GenerateAppManifest(metadata);
        var appxManifest = new Zafiro.DivineBytes.Resource("AppxManifest.xml", ByteSource.FromString(generateAppManifest));
        var extraResources = new List<INamedByteSource> { appxManifest };

        if (sourceIcon.HasValue)
        {
            var assetsResult = MsixAssetGenerator.Generate(sourceIcon.Value);
            if (assetsResult.IsFailure)
                return Result.Failure<IByteSource>($"Failed to generate visual assets: {assetsResult.Error}");

            foreach (var (path, data) in assetsResult.Value)
            {
                extraResources.Add(new Zafiro.DivineBytes.Resource(path.Replace('\\', '/'), ByteSource.FromBytes(data)));
            }
        }

        var generatedPaths = new HashSet<string>(
            extraResources.Select(r => r.Name),
            StringComparer.OrdinalIgnoreCase);

        var dedupedOriginalResources = container.Resources
            .Where(r => !generatedPaths.Contains(r.Name));

        // Build conflict map: subcontainer name → resource names to exclude
        var conflictsByContainer = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in generatedPaths)
        {
            var separatorIndex = path.IndexOf('/');
            if (separatorIndex > 0 && separatorIndex < path.Length - 1)
            {
                var containerName = path[..separatorIndex];
                var resourceName = path[(separatorIndex + 1)..];
                if (!conflictsByContainer.TryGetValue(containerName, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    conflictsByContainer[containerName] = set;
                }
                set.Add(resourceName);
            }
        }

        var filteredSubcontainers = container.Subcontainers.Select(sc =>
            conflictsByContainer.TryGetValue(sc.Name, out var excludeNames)
                ? (INamedContainer)new FilteredNamedContainer(sc, excludeNames)
                : sc);

        var finalContainer = new RootContainer(
            dedupedOriginalResources.Concat(extraResources),
            filteredSubcontainers);

        return FromDirectory(finalContainer, certificate, logger);
    }

    private sealed class FilteredNamedContainer(INamedContainer inner, HashSet<string> excludeNames) : INamedContainer
    {
        public string Name => inner.Name;
        public IEnumerable<INamedByteSource> Resources => inner.Resources.Where(r => !excludeNames.Contains(r.Name));
        public IEnumerable<INamedContainer> Subcontainers => inner.Subcontainers;
    }
}
