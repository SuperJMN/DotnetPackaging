using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public class FromDirectoryOptions
{
    public Maybe<string> Package { get; private set; } = Maybe<string>.None;
    public Maybe<string> PackageId { get; private set; } = Maybe<string>.None;
    public Maybe<string> ExecutableName { get; private set; }
    public Maybe<Architecture> Architecture { get; private set; } = Maybe<Architecture>.None;
    public Maybe<IIcon> Icon { get; private set; } = Maybe<IIcon>.None;
    public Maybe<string> AppName { get; private set; } = Maybe<string>.None;
    public Maybe<Categories> Categories { get; private set; } = Maybe<Categories>.None;
    public Maybe<string> StartupWmClass { get; private set; } = Maybe<string>.None;
    public Maybe<string> Comment { get; private set; } = Maybe<string>.None;
    public Maybe<string> Description { get; private set; } = Maybe<string>.None;
    public Maybe<Uri> Homepage { get; private set; } = Maybe<Uri>.None;
    public Maybe<string> License { get; private set; } = Maybe<string>.None;
    public Maybe<string> Priority { get; private set; } = Maybe<string>.None;
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; private set; } = Maybe<IEnumerable<Uri>>.None;
    public Maybe<string> Maintainer { get; private set; } = Maybe<string>.None;
    public Maybe<string> Summary { get; private set; } = Maybe<string>.None;
    public Maybe<IEnumerable<string>> Keywords { get; private set; } = Maybe<IEnumerable<string>>.None;
    public Maybe<string> Recommends { get; private set; } = Maybe<string>.None;
    public Maybe<string> Section { get; private set; } = Maybe<string>.None;
    public Maybe<string> Version { get; private set; } = Maybe<string>.None;
    public Maybe<string> VcsBrowser { get; private set; } = Maybe<string>.None;
    public Maybe<string> VcsGit { get; private set; } = Maybe<string>.None; 
    public Maybe<long> InstalledSize { get; private set; } = Maybe<long>.None;
    public Maybe<DateTimeOffset> ModificationTime { get; private set; } = Maybe<DateTimeOffset>.None;

    public FromDirectoryOptions WithPackage(string package)
    {
        Package = package;
        return this;
    }

    public FromDirectoryOptions WithPackageId(string packageId)
    {
        PackageId = packageId;
        return this;
    }

    public FromDirectoryOptions WithExecutableName(string executableName)
    {
        ExecutableName = executableName;
        return this;
    }

    public FromDirectoryOptions WithArchitecture(Architecture architecture)
    {
        Architecture = architecture;
        return this;
    }

    public FromDirectoryOptions WithIcon(IIcon icon)
    {
        Icon = Maybe<IIcon>.From(icon);
        return this;
    }

    public FromDirectoryOptions WithAppName(string appName)
    {
        AppName = Maybe<string>.From(appName);
        return this;
    }

    public FromDirectoryOptions WithCategories(Categories categories)
    {
        Categories = Maybe<Categories>.From(categories);
        return this;
    }

    public FromDirectoryOptions WithStartupWmClass(string startupWmClass)
    {
        StartupWmClass = Maybe<string>.From(startupWmClass);
        return this;
    }

    public FromDirectoryOptions WithComment(string comment)
    {
        Comment = Maybe<string>.From(comment);
        return this;
    }

    public FromDirectoryOptions WithDescription(string description)
    {
        Description = Maybe<string>.From(description);
        return this;
    }

    public FromDirectoryOptions WithHomepage(Uri homepage)
    {
        Homepage = Maybe<Uri>.From(homepage);
        return this;
    }

    public FromDirectoryOptions WithLicense(string license)
    {
        License = Maybe<string>.From(license);
        return this;
    }

    public FromDirectoryOptions WithPriority(string priority)
    {
        Priority = Maybe<string>.From(priority);
        return this;
    }

    public FromDirectoryOptions WithScreenshotUrls(IEnumerable<Uri> screenshotUrls)
    {
        ScreenshotUrls = Maybe<IEnumerable<Uri>>.From(screenshotUrls);
        return this;
    }

    public FromDirectoryOptions WithMaintainer(string maintainer)
    {
        Maintainer = Maybe<string>.From(maintainer);
        return this;
    }

    public FromDirectoryOptions WithSummary(string summary)
    {
        Summary = Maybe<string>.From(summary);
        return this;
    }

    public FromDirectoryOptions WithKeywords(IEnumerable<string> keywords)
    {
        Keywords = Maybe<IEnumerable<string>>.From(keywords);
        return this;
    }

    public FromDirectoryOptions WithRecommends(string recommends)
    {
        Recommends = Maybe<string>.From(recommends);
        return this;
    }

    public FromDirectoryOptions WithSection(string section)
    {
        Section = Maybe<string>.From(section);
        return this;
    }

    public FromDirectoryOptions WithVersion(string version)
    {
        Version = Maybe<string>.From(version);
        return this;
    }

    public FromDirectoryOptions WithVcsBrowser(string vcsBrowser)
    {
        VcsBrowser = Maybe<string>.From(vcsBrowser);
        return this;
    }

    public FromDirectoryOptions WithVcsGit(string vcsGit)
    {
        VcsGit = Maybe<string>.From(vcsGit);
        return this;
    }

    public FromDirectoryOptions WithInstalledSize(long installedSize)
    {
        InstalledSize = Maybe<long>.From(installedSize);
        return this;
    }

    public FromDirectoryOptions WithModificationTime(DateTimeOffset modificationTime)
    {
        ModificationTime = Maybe<DateTimeOffset>.From(modificationTime);
        return this;
    }
}