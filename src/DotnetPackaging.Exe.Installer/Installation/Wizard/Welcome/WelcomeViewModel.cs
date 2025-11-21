using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
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
        LoadMetadata = ReactiveUI.ReactiveCommand.CreateFromTask(() => this.payload.GetMetadata());
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata?>(LoadMetadata.Successes());
        this.ValidationRule(model => model.Metadata.Value, m => m is not null, "Metadata is required");

        LoadLogo = ReactiveUI.ReactiveCommand.CreateFromTask(() => payload.GetLogo());

        Logo = LoadLogo
            .Successes()
            .Select(logoBytes => logoBytes.Match(BrandingLogoFactory.FromBytes, () => (IImage?)null))
            .ToReadOnlyReactivePropertySlim();
    }

    public Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveUI.ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }

    public ReactiveUI.ReactiveCommand<Unit, Result<Maybe<IByteSource>>> LoadLogo { get; }

    public ReadOnlyReactivePropertySlim<IImage?> Logo { get; }

    public IObservable<bool> IsValid => this.IsValid();
}
