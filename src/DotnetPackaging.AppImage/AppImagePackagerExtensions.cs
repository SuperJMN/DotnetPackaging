using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage;

public static class AppImagePackagerExtensions
{
    public static IByteSource FromProject(
        this AppImagePackager packager,
        string projectPath,
        Action<AppImagePackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        if (packager == null)
        {
            throw new ArgumentNullException(nameof(packager));
        }

        var metadata = new AppImagePackagerMetadata();
        configure?.Invoke(metadata);

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var log = logger ?? Log.Logger;
        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
        var projectFile = new FileInfo(projectPath);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container =>
            {
                var resolvedOptions = ProjectMetadataDefaults.ResolveFromProject(metadata.PackageOptions, projectFile, log);
                var resolvedMetadata = new AppImagePackagerMetadata();
                resolvedMetadata.PackageOptions.ApplyOverrides(resolvedOptions);
                resolvedMetadata.AppImageOptions.IconNameOverride = metadata.AppImageOptions.IconNameOverride;
                return packager.Pack(container, resolvedMetadata, log);
            });
    }

    public static Task<Result> PackProject(
        this AppImagePackager packager,
        string projectPath,
        string outputPath,
        Action<AppImagePackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = packager.FromProject(projectPath, configure, publishConfigure, logger);
        return source.WriteTo(outputPath);
    }
}
