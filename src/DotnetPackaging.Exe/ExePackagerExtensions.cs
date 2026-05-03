using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Exe.Metadata;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

public static class ExePackagerExtensions
{
    public static IByteSource FromProject(
        this ExePackager packager,
        string projectPath,
        Action<ExePackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        if (packager == null)
        {
            throw new ArgumentNullException(nameof(packager));
        }

        var metadata = new ExePackagerMetadata();
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

        if (metadata.RuntimeIdentifier.HasNoValue && publishRequest.Rid.HasValue)
        {
            metadata.RuntimeIdentifier = publishRequest.Rid;
        }

        ApplyProjectDefaults(metadata, context.Value);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container => packager.Pack(container, metadata));
    }

    public static IByteSource FromPublishedProject(
        this ExePackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        Action<ExePackagerMetadata>? configure = null,
        ILogger? logger = null)
    {
        if (packager == null) throw new ArgumentNullException(nameof(packager));
        if (publishedProject == null) throw new ArgumentNullException(nameof(publishedProject));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var metadata = new ExePackagerMetadata();
        configure?.Invoke(metadata);
        ApplyProjectDefaults(metadata, context);
        return PackagingByteSource.FromResultFactory(() => packager.Pack(publishedProject, metadata));
    }

    public static Task<Result> PackPublishedProject(
        this ExePackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        string outputPath,
        Action<ExePackagerMetadata>? configure = null,
        ILogger? logger = null)
    {
        var source = packager.FromPublishedProject(publishedProject, context, configure, logger);
        return source.WriteTo(outputPath);
    }

    public static Task<Result> PackProject(
        this ExePackager packager,
        string projectPath,
        string outputPath,
        Action<ExePackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = packager.FromProject(projectPath, configure, publishConfigure, logger);
        return source.WriteTo(outputPath);
    }

    private static void ApplyProjectDefaults(ExePackagerMetadata metadata, FileInfo projectFile, ILogger logger)
    {
        if (metadata.ProjectMetadata.HasNoValue)
        {
            metadata.ProjectMetadata = ProjectMetadataReader.TryRead(projectFile, logger);
        }

        if (metadata.ProjectName.HasNoValue)
        {
            metadata.ProjectName = ProjectMetadataDefaults.InferExecutableName(metadata.ProjectMetadata, projectFile);
        }

        if (metadata.OutputName.HasNoValue)
        {
            var projectBase = metadata.ProjectName.GetValueOrDefault(System.IO.Path.GetFileNameWithoutExtension(projectFile.Name));
            metadata.OutputName = Maybe.From(projectBase + ".exe");
        }
    }

    private static void ApplyProjectDefaults(ExePackagerMetadata metadata, ProjectPackagingContext context)
    {
        if (metadata.ProjectMetadata.HasNoValue)
        {
            metadata.ProjectMetadata = context.ProjectMetadata;
        }

        if (metadata.ProjectName.HasNoValue)
        {
            metadata.ProjectName = context.InferExecutableName();
        }

        if (metadata.OutputName.HasNoValue)
        {
            var projectBase = metadata.ProjectName.GetValueOrDefault(System.IO.Path.GetFileNameWithoutExtension(context.ProjectFile.Name));
            metadata.OutputName = Maybe.From(projectBase + ".exe");
        }
    }
}
