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
        var context = ProjectPackagingContext.FromProject(projectPath, log);
        if (context.IsFailure)
        {
            return PackagingByteSource.FromFailure(context.Error);
        }

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container => packager.FromPublishedProject(container, context.Value, metadata, log));
    }

    public static IByteSource FromPublishedProject(
        this AppImagePackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        AppImagePackagerMetadata? metadata = null,
        ILogger? logger = null)
    {
        if (packager == null) throw new ArgumentNullException(nameof(packager));
        if (publishedProject == null) throw new ArgumentNullException(nameof(publishedProject));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var log = logger ?? Log.Logger;
        var source = metadata ?? new AppImagePackagerMetadata();
        var resolvedOptions = context.ResolveFromDirectoryOptions(source.PackageOptions);
        var resolvedMetadata = new AppImagePackagerMetadata();
        resolvedMetadata.PackageOptions.ApplyOverrides(resolvedOptions);
        resolvedMetadata.AppImageOptions.IconNameOverride = source.AppImageOptions.IconNameOverride;
        return PackagingByteSource.FromResultFactory(() => packager.Pack(publishedProject, resolvedMetadata, log));
    }

    public static Task<Result> PackPublishedProject(
        this AppImagePackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        string outputPath,
        AppImagePackagerMetadata? metadata = null,
        ILogger? logger = null)
    {
        var source = packager.FromPublishedProject(publishedProject, context, metadata, logger);
        return source.WriteTo(outputPath);
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
