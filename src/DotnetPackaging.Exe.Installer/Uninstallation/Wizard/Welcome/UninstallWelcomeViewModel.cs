using System.Reactive;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace DotnetPackaging.Exe.Installer.Uninstallation.Wizard.Welcome;

public sealed class UninstallWelcomeViewModel : ReactiveValidationObject, IValidatable
{
    private readonly IInstallerPayload payload;

    public UninstallWelcomeViewModel(IInstallerPayload payload)
    {
        this.payload = payload;
        LoadMetadata = ReactiveCommand.CreateFromTask(() => this.payload.GetMetadata());
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata?>(LoadMetadata.Successes());
        this.ValidationRule(model => model.Metadata.Value, m => m is not null, "Metadata is required");
    }

    public Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }

    public IObservable<bool> IsValid => this.IsValid();
}
