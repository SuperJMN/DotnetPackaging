using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Tests;

public class ContainerOptionsSetup
{
    public bool DetectArchitecture { get; private set; }
    public string Package { get; private set; }
    public string PackageId { get; private set; }
    public string ExecutableName { get; private set; }
    public Maybe<Architecture> Architecture { get; private set; }

    public ContainerOptionsSetup WithPackage(string package)
    {
        this.Package = package;
        return this;
    }

    public ContainerOptionsSetup WithPackageId(string packageId)
    {
        this.PackageId = packageId;
        return this;

    }

    public ContainerOptionsSetup WithExecutableName(string executableName)
    {
        this.ExecutableName = executableName;
        return this;
    }
    
    public ContainerOptionsSetup WithArchitecture(Architecture architecture)
    {
        this.Architecture = architecture;
        return this;
    }


    public ContainerOptionsSetup AutoDetectArchitecture()
    {
        DetectArchitecture = true;
        return this;
    }
}