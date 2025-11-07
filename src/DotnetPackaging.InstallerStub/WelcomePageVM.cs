namespace DotnetPackaging.InstallerStub;

public sealed class WelcomePageVM
{
    public WelcomePageVM(InstallerMetadata metadata)
    {
        Metadata = metadata;
        Heading = $"Welcome to the {metadata.ApplicationName} Setup Wizard";
        Description =
            $"This wizard will guide you through the installation of {metadata.ApplicationName} version {metadata.Version}.";
        AdditionalInfo =
            string.IsNullOrWhiteSpace(metadata.Vendor)
                ? null
                : $"Publisher: {metadata.Vendor}";
    }

    public InstallerMetadata Metadata { get; }

    public string Heading { get; }

    public string Description { get; }

    public string? AdditionalInfo { get; }
}
