namespace DotnetPackaging.AppImage.Tests;

public class ContainerOptionsSetup
{
    private string package;
    private string packageId;
    private string executableName;

    public ContainerOptionsSetup Package(string package)
    {
        this.package = package;
        return this;
    }

    public ContainerOptionsSetup PackageId(string packageId)
    {
        this.packageId = packageId;
        return this;

    }

    public ContainerOptionsSetup ExecutableName(string executableName)
    {
        this.executableName = executableName;
        return this;
    }
}