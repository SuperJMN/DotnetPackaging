using System.Reactive;
using CSharpFunctionalExtensions;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace DotnetPackaging.Exe.Installer.Steps.Welcome;

public sealed class WelcomeViewModel : ReactiveValidationObject, IWelcomeViewModel, IValidatable
{
    public WelcomeViewModel()
    {
        LoadMetadata = ReactiveCommand.Create(LoadMetadataCore);
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata?>(LoadMetadata.Successes());
        this.ValidationRule(model => model.Metadata.Value, m => m is not null, "Metadata is required");
    }

    public Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }
    
    private static Result<InstallerMetadata> LoadMetadataCore()
    {
        var payloadResult = PayloadExtractor.LoadPayload();
        if (payloadResult.IsFailure)
        {
            return Result.Failure<InstallerMetadata>(payloadResult.Error);
        }

        return Result.Success(payloadResult.Value.Metadata);
    }

    public IObservable<bool> IsValid => this.IsValid();
}
