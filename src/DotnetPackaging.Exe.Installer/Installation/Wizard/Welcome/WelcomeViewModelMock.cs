using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Welcome;

public class WelcomeViewModelMock : IWelcomeViewModel
{
    private static readonly byte[] SampleLogo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAoMBgNkHFN0AAAAASUVORK5CYII=");

    public WelcomeViewModelMock()
    {
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata?>(new InstallerMetadata(
            "com.example.app",
            "Example App",
            "1.0.0",
            "Example, Inc.",
            Description: "This is an example app. It does nothing. It's just a demo.",
            HasLogo: true));

        LoadLogo = ReactiveUI.ReactiveCommand.Create(() => Result.Success(Maybe<IByteSource>.From(ByteSource.FromBytes(SampleLogo))));

        Logo = LoadLogo
            .Successes()
            .Select(logoBytes => logoBytes.Match(BrandingLogoFactory.FromBytes, () => (IImage?)null))
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
}
