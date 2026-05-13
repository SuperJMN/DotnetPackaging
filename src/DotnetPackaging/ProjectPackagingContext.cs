using Serilog;
using Zafiro.DivineBytes;
using DotnetProjectKit;

namespace DotnetPackaging;

/// <summary>
/// Project-aware packaging context. It captures the metadata that <c>from-project</c>
/// uses so callers can publish once and package one or more formats without losing
/// project-derived defaults.
/// </summary>
public sealed class ProjectPackagingContext
{
    private readonly ILogger logger;

    private ProjectPackagingContext(
        FileInfo projectFile,
        Maybe<ApplicationInfo> applicationInfo,
        ILogger logger)
    {
        ProjectFile = projectFile;
        ApplicationInfo = applicationInfo;
        this.logger = logger;
    }

    public string ProjectPath => ProjectFile.FullName;
    public FileInfo ProjectFile { get; }
    public Maybe<ApplicationInfo> ApplicationInfo { get; }

    public static Result<ProjectPackagingContext> FromProject(string projectPath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return Result.Failure<ProjectPackagingContext>("Project path is required.");

        var projectFile = new FileInfo(projectPath);
        if (!projectFile.Exists)
            return Result.Failure<ProjectPackagingContext>($"Project file not found: {projectFile.FullName}");

        var log = logger ?? Log.Logger;
        var applicationInfoResult = new ApplicationInfoResolver().Resolve(projectFile.FullName, logger: log);
        if (applicationInfoResult.IsFailure)
        {
            log.Warning(
                "Unable to resolve application info from {ProjectFile}: {Error}",
                projectFile.FullName,
                applicationInfoResult.Error);
            return Result.Success(new ProjectPackagingContext(projectFile, Maybe<ApplicationInfo>.None, log));
        }

        var applicationInfo = applicationInfoResult.Value;
        return Result.Success(new ProjectPackagingContext(projectFile, Maybe<ApplicationInfo>.From(applicationInfo), log));
    }

    public static Result<ProjectPackagingContext> FromApplicationInfo(ApplicationInfo applicationInfo, ILogger? logger = null)
    {
        if (applicationInfo == null)
        {
            throw new ArgumentNullException(nameof(applicationInfo));
        }

        var projectFile = new FileInfo(applicationInfo.ProjectPath);
        if (!projectFile.Exists)
        {
            return Result.Failure<ProjectPackagingContext>($"Project file not found: {projectFile.FullName}");
        }

        return Result.Success(new ProjectPackagingContext(
            projectFile,
            Maybe<ApplicationInfo>.From(applicationInfo),
            logger ?? Log.Logger));
    }

    public FromDirectoryOptions ResolveFromDirectoryOptions(FromDirectoryOptions overrides)
    {
        var withMetadata = new FromDirectoryOptions().ApplyOverrides(overrides);
        ApplicationInfo.Tap(info => withMetadata.WithApplicationInfo(info));
        return ApplicationInfo.HasValue
            ? ApplicationInfoDefaults.ResolveFromApplicationInfo(withMetadata, ApplicationInfo.Value)
            : ApplicationInfoDefaults.ResolveFromProject(withMetadata, ProjectFile, logger);
    }

    public Maybe<string> InferExecutableName() =>
        ApplicationInfo.HasValue
            ? Maybe<string>.From(ApplicationInfo.Value.ExecutableName.Value)
            : ApplicationInfoDefaults.InferExecutableName(ApplicationInfo, ProjectFile);

    public IContainer EnrichPublishedProjectWithProjectAssets(IContainer publishedProject)
    {
        if (publishedProject == null)
        {
            throw new ArgumentNullException(nameof(publishedProject));
        }

        if (HasRootIconCandidate(publishedProject) || ApplicationInfo.HasNoValue || ApplicationInfo.Value.Icon is null)
        {
            return publishedProject;
        }

        var iconPath = ApplicationInfo.Value.Icon.Path;
        var iconName = ResolveRootIconName(iconPath);
        if (iconName is null)
        {
            logger.Debug("Project icon {IconPath} is not a directly embeddable packaging icon.", iconPath);
            return publishedProject;
        }

        logger.Information("Using project icon {IconPath}", iconPath);
        return new RootContainer(
            ReplaceRootResource(publishedProject.Resources, new NamedByteSource(iconName, FileByteSource.OpenRead(iconPath))),
            publishedProject.Subcontainers);
    }

    private static string? ResolveRootIconName(string iconPath)
    {
        var extension = System.IO.Path.GetExtension(iconPath);
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
        {
            return "icon.png";
        }

        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            return "icon.svg";
        }

        return null;
    }

    private static bool HasRootIconCandidate(IContainer container)
    {
        return container.Resources.Any(resource =>
            resource.Name.Equals("icon.png", StringComparison.OrdinalIgnoreCase)
            || resource.Name.Equals("icon.svg", StringComparison.OrdinalIgnoreCase)
            || resource.Name.Equals("icon-256.png", StringComparison.OrdinalIgnoreCase)
            || resource.Name.Equals("icon-512.png", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<INamedByteSource> ReplaceRootResource(
        IEnumerable<INamedByteSource> resources,
        INamedByteSource replacement)
    {
        var replaced = false;
        foreach (var resource in resources)
        {
            if (resource.Name.Equals(replacement.Name, StringComparison.OrdinalIgnoreCase))
            {
                replaced = true;
                yield return replacement;
            }
            else
            {
                yield return resource;
            }
        }

        if (!replaced)
        {
            yield return replacement;
        }
    }
}
