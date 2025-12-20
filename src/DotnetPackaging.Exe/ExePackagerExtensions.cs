using CSharpFunctionalExtensions;
using DotnetPackaging;
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
        var projectFile = new FileInfo(projectPath);

        if (metadata.RuntimeIdentifier.HasNoValue && publishRequest.Rid.HasValue)
        {
            metadata.RuntimeIdentifier = publishRequest.Rid;
        }

        ApplyProjectDefaults(metadata, projectFile, log);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container => packager.Pack(container, metadata));
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
}
