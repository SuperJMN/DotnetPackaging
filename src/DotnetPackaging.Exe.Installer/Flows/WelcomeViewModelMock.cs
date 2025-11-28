using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using Reactive.Bindings;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Flows;

public class WelcomeViewModelMock : IWelcomeViewModel
{

    public WelcomeViewModelMock()
    {
        OperationName = "install";
        Preposition = "on";
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata?>(new InstallerMetadata(
            "com.example.app",
            "Example App",
            "1.0.0",
            "Example, Inc.",
            Description: "This is an example app. It does nothing. It's just a demo.",
            HasLogo: true));
        Title = Metadata.Select(x => x is null ? "" : $"Welcome to {x.ApplicationName}").ToReadOnlyReactivePropertySlim("");

        LoadLogo = ReactiveUI.ReactiveCommand.Create(() => Result.Success(Maybe<IByteSource>.From(SampleData.Logo)));

        Logo = LoadLogo
            .Successes()
            .Select(logoBytes => logoBytes.Match(BrandingLogoFactory.FromBytes, () => null))
            .ToReadOnlyReactivePropertySlim();
    }

   
    public Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveUI.ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; } = ReactiveUI.ReactiveCommand.Create(() => Result.Success(new InstallerMetadata(
        "com.example.app",
        "Example App",
        "1.0.0",
        "Example, Inc.",
        Description: "This is an example app. It does nothing. It's just a demo.",
        HasLogo: true)));

    public ReactiveUI.ReactiveCommand<Unit, Result<Maybe<IByteSource>>> LoadLogo { get; }

    public ReadOnlyReactivePropertySlim<IImage?> Logo { get; }
    public ReadOnlyReactivePropertySlim<string> Title { get; }
    public string OperationName { get; }
    public string Preposition { get; }
}
