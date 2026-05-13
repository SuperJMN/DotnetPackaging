using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Msix.Core.Manifest;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Msix;

public static class MsixPackagerExtensions
{
    public static IByteSource FromProject(
        this MsixPackager packager,
        string projectPath,
        Action<AppManifestMetadata>? metadataConfigure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        Maybe<SigningOptions> signingOptions = default,
        Maybe<byte[]> sourceIcon = default,
        ILogger? logger = null)
    {
        if (packager == null)
            throw new ArgumentNullException(nameof(packager));

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
        var metadata = metadataConfigure != null
            ? BuildMetadata(metadataConfigure)
            : Maybe<AppManifestMetadata>.None;

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container => packager.Pack(container, metadata, signingOptions, sourceIcon, logger));
    }

    public static Task<Result> PackProject(
        this MsixPackager packager,
        string projectPath,
        string outputPath,
        Action<AppManifestMetadata>? metadataConfigure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        Maybe<SigningOptions> signingOptions = default,
        Maybe<byte[]> sourceIcon = default,
        ILogger? logger = null)
    {
        var source = packager.FromProject(projectPath, metadataConfigure, publishConfigure, signingOptions, sourceIcon, logger);
        return source.WriteTo(outputPath);
    }

    public static IByteSource FromPublishedProject(
        this MsixPackager packager,
        IContainer publishedProject,
        Action<AppManifestMetadata>? metadataConfigure = null,
        Maybe<SigningOptions> signingOptions = default,
        Maybe<byte[]> sourceIcon = default,
        ILogger? logger = null)
    {
        if (packager == null)
            throw new ArgumentNullException(nameof(packager));
        if (publishedProject == null)
            throw new ArgumentNullException(nameof(publishedProject));

        var metadata = metadataConfigure != null
            ? BuildMetadata(metadataConfigure)
            : Maybe<AppManifestMetadata>.None;

        return PackagingByteSource.FromResultFactory(() => packager.Pack(publishedProject, metadata, signingOptions, sourceIcon, logger));
    }

    public static Task<Result> PackPublishedProject(
        this MsixPackager packager,
        IContainer publishedProject,
        string outputPath,
        Action<AppManifestMetadata>? metadataConfigure = null,
        Maybe<SigningOptions> signingOptions = default,
        Maybe<byte[]> sourceIcon = default,
        ILogger? logger = null)
    {
        var source = packager.FromPublishedProject(publishedProject, metadataConfigure, signingOptions, sourceIcon, logger);
        return source.WriteTo(outputPath);
    }

    private static Maybe<AppManifestMetadata> BuildMetadata(Action<AppManifestMetadata> configure)
    {
        var metadata = new AppManifestMetadata();
        configure(metadata);
        return Maybe<AppManifestMetadata>.From(metadata);
    }
}
