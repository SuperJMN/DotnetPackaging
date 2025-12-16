using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage;

/// <summary>
/// High-level API for creating AppImage packages from .NET projects.
/// </summary>
public static class AppImagePackager
{
    /// <summary>
    /// Creates a lazy IByteSource that publishes the project and packages it as an AppImage on-demand.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="configure">Optional configuration for package metadata.</param>
    /// <param name="publishConfigure">Optional configuration for the publish operation.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>An IByteSource representing the lazy AppImage package.</returns>
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
                var setup = new FromDirectoryOptions();
                ApplyOptions(options, setup);

                var execRes = await BuildUtils.GetExecutable(container, setup, logger ?? Log.Logger);
                if (execRes.IsFailure) return Result.Failure<IByteSource>(execRes.Error);

                var archRes = await BuildUtils.GetArch(setup, execRes.Value);
                if (archRes.IsFailure) return Result.Failure<IByteSource>(archRes.Error);

                var pm = await BuildUtils.CreateMetadata(setup, container, archRes.Value, execRes.Value, setup.IsTerminal, Maybe<string>.From(projectName), logger ?? Log.Logger);

                // Convert PackageMetadata to AppImageMetadata
                var packageName = pm.Package;
                var appId = pm.Id.GetValueOrDefault($"com.{packageName.ToLowerInvariant()}");

                var metadata = new AppImageMetadata(appId, pm.Name, packageName)
                {
                    Summary = pm.Summary,
                    Comment = pm.Description,
                    Version = Maybe<string>.From(pm.Version),
                    Homepage = pm.Homepage.Map(u => u.ToString()),
                    ProjectLicense = pm.License,
                    Keywords = pm.Keywords,
                    IsTerminal = setup.IsTerminal,
                    Categories = pm.Categories.HasValue
                        ? Maybe<IEnumerable<string>>.From(GetCategoryStrings(pm.Categories.Value))
                        : Maybe<IEnumerable<string>>.None
                };

                var result = await new AppImageFactory().Create(container, metadata);
                if (result.IsFailure) return Result.Failure<IByteSource>(result.Error);
                return await result.Value.ToByteSource();
            });
    }

    /// <summary>
    /// Publishes the project, packages it as an AppImage, and writes to the output path.
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

    private static IEnumerable<string> GetCategoryStrings(Categories categories)
    {
        var list = new List<string> { categories.Main.ToString() };
        list.AddRange(categories.AdditionalCategories.Select(c => c.ToString()));
        return list;
    }
}
