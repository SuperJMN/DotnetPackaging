using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public class ContainerOptionsSetup
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

    public ContainerOptionsSetup WithPackage(string package)
    {
        Package = package;
        return this;
    }

    public ContainerOptionsSetup WithPackageId(string packageId)
    {
        PackageId = packageId;
        return this;
    }

    public ContainerOptionsSetup WithExecutableName(string executableName)
    {
        ExecutableName = executableName;
        return this;
    }

    public ContainerOptionsSetup WithArchitecture(Architecture architecture)
    {
        Architecture = architecture;
        return this;
    }

    public ContainerOptionsSetup WithIcon(IIcon icon)
    {
        Icon = Maybe<IIcon>.From(icon);
        return this;
    }

    public ContainerOptionsSetup WithAppName(string appName)
    {
        AppName = Maybe<string>.From(appName);
        return this;
    }

    public ContainerOptionsSetup WithCategories(Categories categories)
    {
        Categories = Maybe<Categories>.From(categories);
        return this;
    }

    public ContainerOptionsSetup WithStartupWmClass(string startupWmClass)
    {
        StartupWmClass = Maybe<string>.From(startupWmClass);
        return this;
    }

    public ContainerOptionsSetup WithComment(string comment)
    {
        Comment = Maybe<string>.From(comment);
        return this;
    }

    public ContainerOptionsSetup WithDescription(string description)
    {
        Description = Maybe<string>.From(description);
        return this;
    }

    public ContainerOptionsSetup WithHomepage(Uri homepage)
    {
        Homepage = Maybe<Uri>.From(homepage);
        return this;
    }

    public ContainerOptionsSetup WithLicense(string license)
    {
        License = Maybe<string>.From(license);
        return this;
    }

    public ContainerOptionsSetup WithPriority(string priority)
    {
        Priority = Maybe<string>.From(priority);
        return this;
    }

    public ContainerOptionsSetup WithScreenshotUrls(IEnumerable<Uri> screenshotUrls)
    {
        ScreenshotUrls = Maybe<IEnumerable<Uri>>.From(screenshotUrls);
        return this;
    }

    public ContainerOptionsSetup WithMaintainer(string maintainer)
    {
        Maintainer = Maybe<string>.From(maintainer);
        return this;
    }

    public ContainerOptionsSetup WithSummary(string summary)
    {
        Summary = Maybe<string>.From(summary);
        return this;
    }

    public ContainerOptionsSetup WithKeywords(IEnumerable<string> keywords)
    {
        Keywords = Maybe<IEnumerable<string>>.From(keywords);
        return this;
    }

    public ContainerOptionsSetup WithRecommends(string recommends)
    {
        Recommends = Maybe<string>.From(recommends);
        return this;
    }

    public ContainerOptionsSetup WithSection(string section)
    {
        Section = Maybe<string>.From(section);
        return this;
    }

    public ContainerOptionsSetup WithVersion(string version)
    {
        Version = Maybe<string>.From(version);
        return this;
    }

    public ContainerOptionsSetup WithVcsBrowser(string vcsBrowser)
    {
        VcsBrowser = Maybe<string>.From(vcsBrowser);
        return this;
    }

    public ContainerOptionsSetup WithVcsGit(string vcsGit)
    {
        VcsGit = Maybe<string>.From(vcsGit);
        return this;
    }

    public ContainerOptionsSetup WithInstalledSize(long installedSize)
    {
        InstalledSize = Maybe<long>.From(installedSize);
        return this;
    }

    public ContainerOptionsSetup WithModificationTime(DateTimeOffset modificationTime)
    {
        ModificationTime = Maybe<DateTimeOffset>.From(modificationTime);
        return this;
    }
}