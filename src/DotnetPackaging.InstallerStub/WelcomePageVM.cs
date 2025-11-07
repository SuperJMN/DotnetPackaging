using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ReactiveUI;

namespace DotnetPackaging.InstallerStub;

public sealed class WelcomePageVM : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposables = new();
    private readonly Func<InstallerPayload?> payloadProvider;
    private string? errorMessage;

    public WelcomePageVM(
        ReactiveCommand<Unit, Result<InstallerMetadata>> loadMetadata,
        ReactiveProperty<InstallerMetadata?> metadata,
        Func<InstallerPayload?> payloadProvider)
    {
        LoadMetadata = loadMetadata;
        Metadata = metadata;
        this.payloadProvider = payloadProvider;

        CanContinue = Metadata.Changes
            .Select(meta => meta is not null)
            .StartWith(Metadata.Value is not null);

        var loadSubscription = LoadMetadata.Subscribe(result =>
        {
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                Metadata.OnNext(default!);
            }
            else
            {
                ErrorMessage = null;
            }
        });
        disposables.Add(loadSubscription);

        var metadataSubscription = Metadata.Changes.Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(Heading));
            this.RaisePropertyChanged(nameof(Description));
            this.RaisePropertyChanged(nameof(AdditionalInfo));
            this.RaisePropertyChanged(nameof(ApplicationNameDisplay));
            this.RaisePropertyChanged(nameof(VersionDisplay));
            this.RaisePropertyChanged(nameof(VendorDisplay));
        });
        disposables.Add(metadataSubscription);
    }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }

    public ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public IObservable<bool> CanContinue { get; }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => this.RaiseAndSetIfChanged(ref errorMessage, value);
    }

    public string Heading => Metadata.Value is { } meta
        ? $"Welcome to the {meta.ApplicationName} wizard"
        : "Welcome to the installation wizard";

    public string Description => Metadata.Value is { } meta
        ? $"Version {meta.Version}. You can continue once the metadata is loaded."
        : "Load the metadata to get package information before installing.";

    public string? AdditionalInfo => Metadata.Value?.Vendor is { Length: > 0 } vendor
        ? $"Vendor: {vendor}"
        : null;

    public string ApplicationNameDisplay => Metadata.Value is { } meta
        ? $"Application: {meta.ApplicationName}"
        : "Application: (not loaded)";

    public string VersionDisplay => Metadata.Value is { } meta
        ? $"Version: {meta.Version}"
        : "Version: (not loaded)";

    public string VendorDisplay => Metadata.Value?.Vendor is { Length: > 0 } vendor
        ? $"Vendor: {vendor}"
        : "Vendor: (not loaded)";

    public InstallerPayload GetPayloadOrThrow()
    {
        return payloadProvider() ?? throw new InvalidOperationException("No payload has been loaded. Load metadata first.");
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
