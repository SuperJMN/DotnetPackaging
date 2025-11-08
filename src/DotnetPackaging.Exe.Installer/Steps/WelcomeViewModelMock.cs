namespace DotnetPackaging.Exe.Installer.Steps;

public class WelcomeViewModelMock : IWelcomeViewModel
{
    public InstallerMetadata Metadata { get; set; }
}

public interface IWelcomeViewModel
{
    InstallerMetadata Metadata { get; }
}