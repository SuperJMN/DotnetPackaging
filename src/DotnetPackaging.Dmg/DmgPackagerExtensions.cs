using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Dmg;

public static class DmgPackagerExtensions
{
    public static IByteSource FromProject(
        this DmgPackager packager,
        string projectPath,
        Action<DmgPackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        if (packager == null)
        {
            throw new ArgumentNullException(nameof(packager));
        }

        var metadata = new DmgPackagerMetadata();
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
        this DmgPackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        DmgPackagerMetadata? metadata = null,
        ILogger? logger = null)
    {
        if (packager == null) throw new ArgumentNullException(nameof(packager));
        if (publishedProject == null) throw new ArgumentNullException(nameof(publishedProject));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var log = logger ?? Log.Logger;
        var resolved = ResolveFromProject(metadata ?? new DmgPackagerMetadata(), context);
        return PackagingByteSource.FromResultFactory(() => packager.Pack(publishedProject, resolved, log));
    }

    public static Task<Result> PackPublishedProject(
        this DmgPackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        string outputPath,
        DmgPackagerMetadata? metadata = null,
        ILogger? logger = null)
    {
        var source = packager.FromPublishedProject(publishedProject, context, metadata, logger);
        return source.WriteTo(outputPath);
    }

    public static Task<Result> PackProject(
        this DmgPackager packager,
        string projectPath,
        string outputPath,
        Action<DmgPackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = packager.FromProject(projectPath, configure, publishConfigure, logger);
        return source.WriteTo(outputPath);
    }

    private static DmgPackagerMetadata ResolveFromProject(DmgPackagerMetadata source, FileInfo projectFile, ILogger logger)
    {
        var projectMetadata = ProjectMetadataReader.TryRead(projectFile, logger);
        var inferred = ProjectMetadataDefaults.InferExecutableName(projectMetadata, projectFile);

        return ResolveFromProject(source, inferred);
    }

    private static DmgPackagerMetadata ResolveFromProject(DmgPackagerMetadata source, ProjectPackagingContext context)
    {
        var inferred = context.InferExecutableName();
        return ResolveFromProject(source, inferred);
    }

    private static DmgPackagerMetadata ResolveFromProject(DmgPackagerMetadata source, Maybe<string> inferred)
    {
        return new DmgPackagerMetadata
        {
            VolumeName = source.VolumeName.Or(inferred),
            ExecutableName = source.ExecutableName.Or(inferred),
            Compress = source.Compress,
            AddApplicationsSymlink = source.AddApplicationsSymlink,
            IncludeDefaultLayout = source.IncludeDefaultLayout,
            Icon = source.Icon
        };
    }
}
