using CSharpFunctionalExtensions;
using Reactive.Bindings;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Classic;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace DotnetPackaging.Exe.Installer;

public sealed class MainViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposables = new();
    private InstallerMetadata metadata = new("app", "App", "1.0.0", "Unknown");
    private readonly InstallWizard installWizard;
    private readonly IDialog dialog;
    private readonly INotificationService notificationService;
    private readonly Action onCancel;
    private InstallerPayload? currentPayload;

    public MainViewModel(InstallWizard installWizard, IDialog dialog, INavigator navigator, INotificationService notificationService,
        Action onCancel)
    {
        Navigator = navigator;
        this.installWizard = installWizard;
        this.dialog = dialog;
        this.notificationService = notificationService;
        this.onCancel = onCancel;

        LoadMetadata = ReactiveCommand.Create(LoadMetadataCore);
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata?>(LoadMetadata.Successes());


        LoadWizard = ReactiveCommand.CreateFromTask(StartWizard);
        LoadMetadata.ToSignal().InvokeCommand(LoadWizard);
    }

    public ICommand? LoadWizard { get; set; }

    private Task<Maybe<Unit>> StartWizard()
    {
        var wizard = installWizard.CreateWizard(Metadata.Value!);

        return wizard.Navigate(Navigator, async (_, _) =>
        {
            var result = await dialog.ShowConfirmation("Cancel Installation",
                "Are you sure you want to cancel the installation?");
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

    public Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }

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
  
    public void Dispose()
    {
        disposables.Dispose();
    }
}