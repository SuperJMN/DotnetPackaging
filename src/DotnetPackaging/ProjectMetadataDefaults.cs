namespace DotnetPackaging;

public static class ProjectMetadataDefaults
{
    public static FromDirectoryOptions ResolveFromProject(FromDirectoryOptions overrides, FileInfo projectFile, ILogger logger)
    {
        if (overrides == null)
        {
            throw new ArgumentNullException(nameof(overrides));
        }

        if (projectFile == null)
        {
            throw new ArgumentNullException(nameof(projectFile));
        }

        var resolved = new FromDirectoryOptions().ApplyOverrides(overrides);
        var projectMetadata = overrides.ProjectMetadata.HasValue
            ? overrides.ProjectMetadata
            : ProjectMetadataReader.TryRead(projectFile, logger);

        projectMetadata.Execute(metadata => resolved.WithProjectMetadata(metadata));

        if (resolved.ExecutableName.HasNoValue)
        {
            var inferredExecutable = InferExecutableName(projectMetadata, projectFile);
            inferredExecutable.Execute(name => resolved.WithExecutableName(name));
        }

        return resolved;
    }

    public static Maybe<string> InferExecutableName(Maybe<ProjectMetadata> projectMetadata, FileInfo projectFile)
    {
        var fallback = Maybe.From(Path.GetFileNameWithoutExtension(projectFile.Name));
        return projectMetadata.Bind(metadata => metadata.AssemblyName).Or(fallback);
    }
}
