namespace DotnetPackaging.AppImage.Kernel;

public class Application : IApplication
{
    public PackageMetadata Metadata { get; set; }
    public IRoot Contents { get; set; }
}