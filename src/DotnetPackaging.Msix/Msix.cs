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

        var finalContainer = new RootContainer(
            container.Resources.Concat(extraResources),
            container.Subcontainers);

        return FromDirectory(finalContainer, certificate, logger);
    }
}
