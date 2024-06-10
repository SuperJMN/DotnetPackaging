namespace DotnetPackaging;

public class FromDirectoryOptions
{
    public Maybe<string> Package { get; private set; } = Maybe<string>.None;
    public Maybe<string> Id { get; private set; } = Maybe<string>.None;
    public Maybe<string> ExecutableName { get; private set; }
    public Maybe<Architecture> Architecture { get; private set; } = Maybe<Architecture>.None;
    public Maybe<IIcon> Icon { get; private set; } = Maybe<IIcon>.None;
    
    /// <summary>
    /// Application name (AppName)
    /// </summary>
    public Maybe<string> Name { get; private set; } = Maybe<string>.None;
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
        if (string.IsNullOrWhiteSpace(package))
        {
            throw new ArgumentException("Can't be null or empty", package);
        }
        
        Package = package;
        return this;
    }

    public FromDirectoryOptions WithId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Can't be null or empty", packageId);
        }
        
        Id = packageId;
        return this;
    }

    public FromDirectoryOptions WithExecutableName(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            throw new ArgumentException("Can't be null or empty", executableName);
        }
        
        ExecutableName = executableName;
        return this;
    }

    public FromDirectoryOptions WithArchitecture(Architecture architecture)
    {
        Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        return this;
    }

    public FromDirectoryOptions WithIcon(IIcon icon)
    {
        if (icon == null)
        {
            throw new ArgumentNullException(nameof(icon));
        }

        Icon = Maybe<IIcon>.From(icon);
        return this;
    }

    public FromDirectoryOptions WithName(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("Can't be null or empty", appName);
        }
        
        Name = Maybe<string>.From(appName);
        return this;
    }

    public FromDirectoryOptions WithCategories(Categories categories)
    {
        if (categories == null)
        {
            throw new ArgumentNullException(nameof(categories));
        }

        Categories = Maybe<Categories>.From(categories);
        return this;
    }

    public FromDirectoryOptions WithStartupWmClass(string startupWmClass)
    {
        if (string.IsNullOrWhiteSpace(startupWmClass))
        {
            throw new ArgumentException("Can't be null or empty", startupWmClass);
        }

        
        StartupWmClass = Maybe<string>.From(startupWmClass);
        return this;
    }

    public FromDirectoryOptions WithComment(string comment)
    {
        if (comment == null)
        {
            throw new ArgumentNullException(nameof(comment));
        }

        Comment = Maybe<string>.From(comment);
        return this;
    }

    public FromDirectoryOptions WithDescription(string description)
    {
        if (description == null)
        {
            throw new ArgumentNullException(nameof(description));
        }

        Description = Maybe<string>.From(description);
        return this;
    }

    public FromDirectoryOptions WithHomepage(Uri homepage)
    {
        if (homepage == null)
        {
            throw new ArgumentNullException(nameof(homepage));
        }

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
        if (string.IsNullOrWhiteSpace(maintainer))
        {
            throw new ArgumentException("Can't be null or empty", maintainer);
        }
        
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