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

        projectMetadata.Execute(metadata =>
        {
            resolved.WithProjectMetadata(metadata);

            if (resolved.Description.HasNoValue)
            {
                metadata.Description.Execute(d => resolved.WithDescription(d));
            }
            if (resolved.Maintainer.HasNoValue)
            {
                metadata.Authors.Execute(a => resolved.WithMaintainer(a));
            }
            if (resolved.License.HasNoValue)
            {
                metadata.PackageLicenseExpression.Execute(l => resolved.WithLicense(l));
            }
            if (resolved.Vendor.HasNoValue)
            {
                metadata.Company.Execute(c => resolved.WithVendor(c));
            }
            if (resolved.Url.HasNoValue)
            {
                metadata.PackageProjectUrl
                    .Or(metadata.RepositoryUrl)
                    .Bind(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Maybe<Uri>.From(uri) : Maybe<Uri>.None)
                    .Execute(uri => resolved.WithUrl(uri));
            }
        });

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
