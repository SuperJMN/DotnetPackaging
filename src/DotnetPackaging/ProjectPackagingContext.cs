using Serilog;

namespace DotnetPackaging;

/// <summary>
/// Project-aware packaging context. It captures the metadata that <c>from-project</c>
/// uses so callers can publish once and package one or more formats without losing
/// project-derived defaults.
/// </summary>
public sealed class ProjectPackagingContext
{
    private readonly ILogger logger;

    private ProjectPackagingContext(FileInfo projectFile, Maybe<ProjectMetadata> projectMetadata, ILogger logger)
    {
        ProjectFile = projectFile;
        ProjectMetadata = projectMetadata;
        this.logger = logger;
    }

    public string ProjectPath => ProjectFile.FullName;
    public FileInfo ProjectFile { get; }
    public Maybe<ProjectMetadata> ProjectMetadata { get; }

    public static Result<ProjectPackagingContext> FromProject(string projectPath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return Result.Failure<ProjectPackagingContext>("Project path is required.");

        var projectFile = new FileInfo(projectPath);
        if (!projectFile.Exists)
            return Result.Failure<ProjectPackagingContext>($"Project file not found: {projectFile.FullName}");

        var log = logger ?? Log.Logger;
        var metadata = ProjectMetadataReader.TryRead(projectFile, log);
        return Result.Success(new ProjectPackagingContext(projectFile, metadata, log));
    }

    public FromDirectoryOptions ResolveFromDirectoryOptions(FromDirectoryOptions overrides)
    {
        var withMetadata = new FromDirectoryOptions().ApplyOverrides(overrides);
        ProjectMetadata.Execute(metadata => withMetadata.WithProjectMetadata(metadata));
        return ProjectMetadataDefaults.ResolveFromProject(withMetadata, ProjectFile, logger);
    }

    public Maybe<string> InferExecutableName() =>
        ProjectMetadataDefaults.InferExecutableName(ProjectMetadata, ProjectFile);
}
