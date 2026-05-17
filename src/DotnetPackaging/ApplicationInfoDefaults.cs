using DotnetProjectKit;

namespace DotnetPackaging;

public static class ApplicationInfoDefaults
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

        var applicationInfoResult = new ApplicationInfoResolver().Resolve(projectFile.FullName, logger: logger);
        return applicationInfoResult.IsSuccess
            ? ResolveFromApplicationInfo(overrides, applicationInfoResult.Value)
            : ResolveWithoutApplicationInfo(overrides, projectFile);
    }

    public static FromDirectoryOptions ResolveFromApplicationInfo(FromDirectoryOptions overrides, ApplicationInfo applicationInfo)
    {
        if (overrides == null)
        {
            throw new ArgumentNullException(nameof(overrides));
        }

        if (applicationInfo == null)
        {
            throw new ArgumentNullException(nameof(applicationInfo));
        }

        var resolved = new FromDirectoryOptions().ApplyOverrides(overrides);

        if (resolved.ApplicationInfo.HasNoValue)
        {
            resolved.WithApplicationInfo(applicationInfo);
        }

        if (resolved.Name.HasNoValue)
        {
            resolved.WithName(applicationInfo.DisplayName.Value);
        }

        if (resolved.Id.HasNoValue)
        {
            resolved.WithId((applicationInfo.PackageId ?? applicationInfo.PackageName).Value);
        }

        if (resolved.Description.HasNoValue && applicationInfo.Description is not null)
        {
            resolved.WithDescription(applicationInfo.Description.Value);
        }

        if (resolved.Comment.HasNoValue && applicationInfo.Description is not null)
        {
            resolved.WithComment(applicationInfo.Description.Value);
        }

        if (resolved.Maintainer.HasNoValue && applicationInfo.Authors is not null)
        {
            resolved.WithMaintainer(applicationInfo.Authors.Value);
        }

        if (resolved.License.HasNoValue && applicationInfo.PackageLicenseExpression is not null)
        {
            resolved.WithLicense(applicationInfo.PackageLicenseExpression.Value);
        }

        if (resolved.Vendor.HasNoValue && applicationInfo.Company is not null)
        {
            resolved.WithVendor(applicationInfo.Company.Value);
        }

        if (resolved.Version.HasNoValue)
        {
            resolved.WithVersion(applicationInfo.Version.Value);
        }

        if (resolved.Url.HasNoValue)
        {
            ResolveUrl(applicationInfo).Tap(url => resolved.WithUrl(url));
        }

        if (resolved.ExecutableName.HasNoValue)
        {
            resolved.WithExecutableName(applicationInfo.ExecutableName.Value);
        }

        if (resolved.IsTerminal.HasNoValue && applicationInfo.OutputType is not null)
        {
            resolved.WithIsTerminal(string.Equals(applicationInfo.OutputType.Value, "Exe", StringComparison.OrdinalIgnoreCase));
        }

        if (resolved.StartupWmClass.HasNoValue && applicationInfo.StartupWmClass is not null)
        {
            resolved.WithStartupWmClass(applicationInfo.StartupWmClass.Value);
        }

        return resolved;
    }

    public static Maybe<string> InferExecutableName(Maybe<ApplicationInfo> applicationInfo, FileInfo projectFile)
    {
        if (applicationInfo.HasValue)
        {
            return Maybe<string>.From(applicationInfo.Value.ExecutableName.Value);
        }

        return Maybe.From(Path.GetFileNameWithoutExtension(projectFile.Name));
    }

    private static FromDirectoryOptions ResolveWithoutApplicationInfo(FromDirectoryOptions overrides, FileInfo projectFile)
    {
        var resolved = new FromDirectoryOptions().ApplyOverrides(overrides);
        if (resolved.ExecutableName.HasNoValue)
        {
            resolved.WithExecutableName(Path.GetFileNameWithoutExtension(projectFile.Name));
        }

        return resolved;
    }

    private static Maybe<Uri> ResolveUrl(ApplicationInfo applicationInfo)
    {
        var url = applicationInfo.PackageProjectUrl?.Value ?? applicationInfo.RepositoryUrl?.Value;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Maybe<Uri>.From(uri)
            : Maybe<Uri>.None;
    }
}
