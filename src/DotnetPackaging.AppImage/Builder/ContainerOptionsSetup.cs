using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Builder;

public class ContainerOptionsSetup
{
    public bool DetectArchitecture { get; private set; }
    public string Package { get; private set; }
    public string PackageId { get; private set; }
    public string ExecutableName { get; private set; }
    public Maybe<Architecture> Architecture { get; private set; }
    public Maybe<IIcon> Icon { get; private set; } = Maybe<IIcon>.None;
    public bool DetectIcon { get; set; }
    public Maybe<string> AppName { get; set; }
    public Maybe<Categories> Categories { get; set; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<string> Comment { get; set; }
    public Maybe<string> Description { get; set; }
    public Maybe<Uri> Homepage { get; set; }
    public Maybe<string> License { get; set; }
    public Maybe<string> Priority { get; set; }
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; set; }
    public Maybe<string> Maintainer { get; set; }
    public Maybe<string> Summary { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; set; }
    public Maybe<string> Recommends { get; set; }
    public Maybe<string> Section { get; set; }
    public Maybe<string> Version { get; set; }
    public Maybe<string> VcsBrowser { get; set; }
    public Maybe<string> VcsGit { get; set; }
    public Maybe<long> InstalledSize { get; set; }
    public Maybe<DateTimeOffset> ModificationTime { get; set; }

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


    public ContainerOptionsSetup AutoDetectArchitecture()
    {
        DetectArchitecture = true;
        return this;
    }

    public ContainerOptionsSetup AutoDetectIcon()
    {
        DetectIcon = true;
        return this;
    }
}