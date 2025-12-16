using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Msix;

/// <summary>
/// High-level API for creating MSIX packages from .NET projects.
/// </summary>
public static class MsixProjectPackager
{
    /// <summary>
    /// Creates a lazy IByteSource that publishes the project and packages it as an MSIX on-demand.
    /// </summary>
    public static IByteSource FromProject(
        string projectPath,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container => Msix.FromDirectory(container, Maybe<ILogger>.From(logger)));
    }

    /// <summary>
    /// Publishes the project, packages it as an MSIX, and writes to the output path.
    /// </summary>
    public static async Task<Result> PackProject(
        string projectPath,
        string outputPath,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = FromProject(projectPath, publishConfigure, logger);
        return await source.WriteTo(outputPath);
    }
}
