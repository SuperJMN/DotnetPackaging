using System.Text;
using System.Xml;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core;
using DotnetPackaging.Msix.Core.Manifest;
using File = System.IO.File;

namespace DotnetPackaging.Msix;

public class Msix
{
    public static Result<IByteSource> FromDirectory(IContainer container, Maybe<ILogger> logger)
    {
        return new MsixPackager(logger).Pack(container);
    }

    public static Result<IByteSource> FromDirectoryAndMetadata(IContainer container, AppManifestMetadata metadata, Maybe<ILogger> logger)
    {
        var generateAppManifest = AppManifestGenerator.GenerateAppManifest(metadata);
        var appxManifiest = new Zafiro.DivineBytes.Resource("AppxManifest.xml", ByteSource.FromString(generateAppManifest));
        var finalContainer = new RootContainer(container.Resources.Append(appxManifiest), container.Subcontainers);
        return FromDirectory(finalContainer, logger);
    }
}