using System.Text;
using System.Xml;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core;
using DotnetPackaging.Msix.Core.Manifest;

namespace DotnetPackaging.Msix;

public class Msix
{
    public static Result<IByteSource> FromDirectory(INamedContainer container, Maybe<ILogger> logger)
    {
        return new MsixPackager(logger).Pack(container);
    }
    
    // TODO: Fix this
    // public static Result<IByteSource> FromDirectoryAndMetadata(IContainer container, AppManifestMetadata metadata, Maybe<ILogger> logger)
    // {
    //     var generateAppManifest = AppManifestGenerator.GenerateAppManifest(metadata);
    //     var appxManifiest = ByteSource.FromString(generateAppManifest, Encoding.UTF8);
    //     var dir = Container.Create("metadata", new File("AppxManifest.xml", appxManifiest));
    //     
    //     var merged = Dir.Combine("merged", container, dir);
    //     return new MsixPackager(logger).Pack(merged);
    // }
}