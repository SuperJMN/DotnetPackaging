using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using DotnetPackaging.Rpm.Builder;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm;

/// <summary>
/// High-level API for creating RPM packages from .NET projects.
/// </summary>
public static class RpmPackager
{
    /// <summary>
    /// Creates a lazy IByteSource that publishes the project and packages it as an RPM on-demand.
    /// </summary>
    public static IByteSource FromProject(
        string projectPath,
        Action<FromDirectoryOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var options = new FromDirectoryOptions();
        configure?.Invoke(options);

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
        var projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            async container =>
            {
                var result = await RpmFile.From()
                    .Container(container, projectName)
                    .Configure(o => ApplyOptions(options, o))
                    .Build();

                return result.Map(rpmFile =>
                    ByteSource.FromStreamFactory(() => System.IO.File.OpenRead(rpmFile.FullName)));
            });
    }

    /// <summary>
    /// Publishes the project, packages it as an RPM, and writes to the output path.
    /// </summary>
    public static async Task<Result> PackProject(
        string projectPath,
        string outputPath,
        Action<FromDirectoryOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = FromProject(projectPath, configure, publishConfigure, logger);
        return await source.WriteTo(outputPath);
    }

    private static void ApplyOptions(FromDirectoryOptions source, FromDirectoryOptions target)
    {
        if (source.Name.HasValue) target.WithName(source.Name.Value);
        if (source.Version.HasValue) target.WithVersion(source.Version.Value);
        if (source.Summary.HasValue) target.WithSummary(source.Summary.Value);
        if (source.Description.HasValue) target.WithDescription(source.Description.Value);
        if (source.Comment.HasValue) target.WithComment(source.Comment.Value);
        if (source.Homepage.HasValue) target.WithHomepage(source.Homepage.Value);
        if (source.License.HasValue) target.WithLicense(source.License.Value);
        if (source.Architecture.HasValue) target.WithArchitecture(source.Architecture.Value);
        if (source.Icon.HasValue) target.WithIcon(source.Icon.Value);
        if (source.Categories.HasValue) target.WithCategories(source.Categories.Value);
        if (source.Keywords.HasValue) target.WithKeywords(source.Keywords.Value);
        if (source.ExecutableName.HasValue) target.WithExecutableName(source.ExecutableName.Value);
        if (source.Id.HasValue) target.WithId(source.Id.Value);
        if (source.StartupWmClass.HasValue) target.WithStartupWmClass(source.StartupWmClass.Value);
        if (source.ProjectMetadata.HasValue) target.WithProjectMetadata(source.ProjectMetadata.Value);
        target.WithIsTerminal(source.IsTerminal);
    }
}
