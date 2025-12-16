using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Builder;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb;

/// <summary>
/// High-level API for creating Debian packages from .NET projects.
/// </summary>
public static class DebPackager
{
    /// <summary>
    /// Creates a lazy IByteSource that publishes the project and packages it as a .deb on-demand.
    /// The publish operation occurs when the bytes are consumed.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="configure">Optional configuration for package metadata.</param>
    /// <param name="publishConfigure">Optional configuration for the publish operation.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>An IByteSource representing the lazy .deb package.</returns>
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
                var result = await DebFile.From()
                    .Container(container, projectName)
                    .Configure(o => ApplyOptions(options, o))
                    .Build();

                return result.Map(deb => DebMixin.ToByteSource(deb));
            });
    }

    /// <summary>
    /// Publishes the project, packages it as a .deb, and writes to the output path.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="outputPath">Path for the output .deb file.</param>
    /// <param name="configure">Optional configuration for package metadata.</param>
    /// <param name="publishConfigure">Optional configuration for the publish operation.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>A Result indicating success or failure.</returns>
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
        if (source.Maintainer.HasValue) target.WithMaintainer(source.Maintainer.Value);
        if (source.Section.HasValue) target.WithSection(source.Section.Value);
        if (source.Priority.HasValue) target.WithPriority(source.Priority.Value);
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
