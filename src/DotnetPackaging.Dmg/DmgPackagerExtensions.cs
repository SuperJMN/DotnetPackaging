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
        var projectFile = new FileInfo(projectPath);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container =>
            {
                var resolved = ResolveFromProject(metadata, projectFile, log);
                return packager.Pack(container, resolved, log);
            });
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
