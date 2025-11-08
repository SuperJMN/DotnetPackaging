namespace DotnetPackaging.Exe.Installer.Steps.Welcome;

public class WelcomeViewModelMock : IWelcomeViewModel
{
    public InstallerMetadata Metadata { get; } = new InstallerMetadata(
        "com.example.app",
        "Example App",
        "1.0.0",
        "Example, Inc.", Description: "");
}

public interface IWelcomeViewModel
{
    InstallerMetadata Metadata { get; }
}