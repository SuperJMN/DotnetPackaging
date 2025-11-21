using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.UI;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Welcome;

public sealed class WelcomeViewModel : ReactiveValidationObject, IWelcomeViewModel, IValidatable
{
    private readonly IInstallerPayload payload;

    public WelcomeViewModel(IInstallerPayload payload)
    {
        this.payload = payload;
        LoadMetadata = ReactiveCommand.CreateFromTask(() => this.payload.GetMetadata());
        Metadata = new ReactiveProperty<InstallerMetadata?>(LoadMetadata.Successes());
        this.ValidationRule(model => model.Metadata.Value, m => m is not null, "Metadata is required");

        LoadLogo = ReactiveCommand.CreateFromTask(() => payload.GetLogo());

        Logo = LoadLogo
            .Successes()
            .Select(logoBytes => logoBytes.Match(BrandingLogoFactory.FromBytes, () => (IBitmap?)null))
            .ToReadOnlyReactivePropertySlim();
    }

    public ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }

    public ReactiveCommand<Unit, Result<Maybe<IByteSource>>> LoadLogo { get; }

    public ReadOnlyReactivePropertySlim<IBitmap?> Logo { get; }

    public IObservable<bool> IsValid => this.IsValid();
}
