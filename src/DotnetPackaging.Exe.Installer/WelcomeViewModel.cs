using System.Reactive.Disposables;
using ReactiveUI;

namespace DotnetPackaging.Exe.Installer;

public sealed class WelcomeViewModel : ReactiveObject
{
    public InstallerMetadata Metadata { get; }
    private readonly Func<InstallerPayload?> payloadProvider;
    private string? errorMessage;

    public WelcomeViewModel(InstallerMetadata metadata, Func<InstallerPayload?> payloadProvider)
    {
        Metadata = metadata;
        this.payloadProvider = payloadProvider;
    }


    public string? ErrorMessage
    {
        get => errorMessage;
        private set => this.RaiseAndSetIfChanged(ref errorMessage, value);
    }

    public string Heading => Metadata is { } meta
        ? $"Welcome to the {meta.ApplicationName} wizard"
        : "Welcome to the installation wizard";

    public string Description => Metadata is { } meta
        ? $"Version {meta.Version}. You can continue once the metadata is loaded."
        : "Load the metadata to get package information before installing.";

    public string? AdditionalInfo => Metadata.Vendor is { Length: > 0 } vendor
        ? $"Vendor: {vendor}"
        : null;

    public string ApplicationNameDisplay => Metadata is { } meta
        ? $"Application: {meta.ApplicationName}"
        : "Application: (not loaded)";

    public string VersionDisplay => Metadata is { } meta
        ? $"Version: {meta.Version}"
        : "Version: (not loaded)";

    public string VendorDisplay => Metadata.Vendor is { Length: > 0 } vendor
        ? $"Vendor: {vendor}"
        : "Vendor: (not loaded)";

    public InstallerPayload GetPayloadOrThrow()
    {
        return payloadProvider() ?? throw new InvalidOperationException("No payload has been loaded. Load metadata first.");
    }
}
