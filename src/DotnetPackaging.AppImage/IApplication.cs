namespace DotnetPackaging.AppImage.Tests;

public interface IApplication
{
    public PackageMetadata Metadata { get; set; }
    public IRoot Contents { get; set; }
}