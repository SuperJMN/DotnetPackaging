using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.InstallerStub;

public sealed class WizardViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposables = new();
    private InstallerMetadata metadata = new("app", "App", "1.0.0", "Unknown");
    private readonly INotificationService notificationService;
    private InstallerPayload? currentPayload;

    public WizardViewModel(IDialog dialog, INavigator navigator, INotificationService notificationService, Action onCancel)
    {
        Navigator = navigator;
        this.notificationService = notificationService;

        LoadMetadata = ReactiveCommand.Create(LoadMetadataCore);

        Metadata = new ReactiveProperty<InstallerMetadata?>(
            LoadMetadata.Successes().Select(meta => (InstallerMetadata?)meta),
            null);

        var failureSubscription = LoadMetadata
            .Failures()
            .Subscribe(_ => Metadata.OnNext(default!));
        disposables.Add(failureSubscription);

        var metadataSubscription = Metadata.Changes.Subscribe(meta =>
        {
            metadata = meta ?? new InstallerMetadata("app", "App", "1.0.0", "Unknown");
            this.RaisePropertyChanged(nameof(ApplicationName));
        });
        disposables.Add(metadataSubscription);

        disposables.Add(Metadata);

        var wizard = CreateWizard();
        _ = wizard.Navigate(Navigator, async (_, _) =>
        {
            var result = await dialog.ShowConfirmation("Cancel Installation", "Are you sure you want to cancel the installation?");
            result.Tap(b =>
            {
                if (b)
                {
                    onCancel();
                }
            });

            return result.GetValueOrDefault(false);
        });
    }

    public string ApplicationName => metadata.ApplicationName;

    public INavigator Navigator { get; }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }

    public ReactiveProperty<InstallerMetadata?> Metadata { get; }

    private Result<InstallerMetadata> LoadMetadataCore()
    {
        var payloadResult = PayloadExtractor.LoadPayload();
        if (payloadResult.IsFailure)
        {
            currentPayload = null;
            return Result.Failure<InstallerMetadata>(payloadResult.Error);
        }

        currentPayload = payloadResult.Value;
        return Result.Success(currentPayload.Metadata);
    }

    private SlimWizard<string> CreateWizard()
    {
        var welcome = new WelcomePageVM(LoadMetadata, Metadata, () => currentPayload);

        return WizardBuilder
            .StartWith(() => welcome, "Welcome")
            .Next(page => page.GetPayloadOrThrow(), "Continue")
            .When(page => page.CanContinue)
            .Then(payload =>
            {
                var installDir = GetDefaultInstallDirectory(payload.Metadata);
                return new OptionsPageVM(installDir, payload.Metadata);
            }, "Destination")
            .NextCommand((page, payload) =>
            {
                page.ResetProgress();
                var installCommand = EnhancedCommand.Create(() => InstallApplicationAsync(payload, page.InstallDirectory, page.ProgressObserver), text: "Instalar");
                page.Track(installCommand.HandleErrorsWith(notificationService));
                return installCommand;
            })
            .Then(result => new CompletionPageVM(result), "Completed")
            .Next(_ => "Done", "Cerrar")
            .Always()
            .WithCompletionFinalStep();
    }

    private static Task<Result<InstallationResult>> InstallApplicationAsync(InstallerPayload payload, string installDir, IObserver<Progress> progressObserver)
    {
        return Task.Run(() =>
            PayloadExtractor.CopyContentTo(payload, installDir, progressObserver)
                .Bind(() => Installer.Install(installDir, payload.Metadata))
                .Map(exePath => new InstallationResult(payload.Metadata, installDir, exePath)));
    }

    private static string GetDefaultInstallDirectory(InstallerMetadata installerMetadata)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        var vendorPart = SanitizePathPart(installerMetadata.Vendor);
        var appPart = SanitizePathPart(installerMetadata.ApplicationName);

        return string.Equals(vendorPart, appPart, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(vendorPart)
            ? Path.Combine(baseDir, appPart)
            : Path.Combine(baseDir, vendorPart, appPart);
    }

    private static string SanitizePathPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "App";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "App" : sanitized;
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
