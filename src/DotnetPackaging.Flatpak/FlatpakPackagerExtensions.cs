using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

public static class FlatpakPackagerExtensions
{
    public static IByteSource FromProject(
        this FlatpakPackager packager,
        string projectPath,
        Action<FlatpakPackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        if (packager == null)
        {
            throw new ArgumentNullException(nameof(packager));
        }

        var metadata = new FlatpakPackagerMetadata();
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
                var resolved = ProjectMetadataDefaults.ResolveFromProject(metadata.PackageOptions, projectFile, log);
                var resolvedMetadata = new FlatpakPackagerMetadata();
                resolvedMetadata.PackageOptions.ApplyOverrides(resolved);
                resolvedMetadata.FlatpakOptions = new FlatpakOptions
                {
                    Runtime = metadata.FlatpakOptions.Runtime,
                    Sdk = metadata.FlatpakOptions.Sdk,
                    Branch = metadata.FlatpakOptions.Branch,
                    RuntimeVersion = metadata.FlatpakOptions.RuntimeVersion,
                    Shared = metadata.FlatpakOptions.Shared.ToArray(),
                    Sockets = metadata.FlatpakOptions.Sockets.ToArray(),
                    Devices = metadata.FlatpakOptions.Devices.ToArray(),
                    Filesystems = metadata.FlatpakOptions.Filesystems.ToArray(),
                    ArchitectureOverride = metadata.FlatpakOptions.ArchitectureOverride,
                    CommandOverride = metadata.FlatpakOptions.CommandOverride
                };
                return packager.Pack(container, resolvedMetadata, log);
            });
    }

    public static Task<Result> PackProject(
        this FlatpakPackager packager,
        string projectPath,
        string outputPath,
        Action<FlatpakPackagerMetadata>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = packager.FromProject(projectPath, configure, publishConfigure, logger);
        return source.WriteTo(outputPath);
    }
}
