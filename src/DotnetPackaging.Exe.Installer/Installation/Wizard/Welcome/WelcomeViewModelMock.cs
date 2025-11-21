using System;
using System.Reactive;
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using ReactiveUI;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Welcome;

public class WelcomeViewModelMock : IWelcomeViewModel
{
    private static readonly byte[] SampleLogo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAoMBgNkHFN0AAAAASUVORK5CYII=");

    public WelcomeViewModelMock()
    {
        Metadata = new ReactiveProperty<InstallerMetadata>(new InstallerMetadata(
            "com.example.app",
            "Example App",
            "1.0.0",
            "Example, Inc.",
            Description: "This is an example app. It does nothing. It's just a demo.",
            HasLogo: true));

        LoadLogo = ReactiveCommand.Create(() => Result.Success(Maybe<IByteSource>.From(ByteSource.FromBytes(SampleLogo))));

        Logo = LoadLogo
            .Successes()
            .Select(logoBytes => logoBytes.Match(BrandingLogoFactory.FromBytes, () => (IBitmap?)null))
            .ToReadOnlyReactivePropertySlim();
    }

    public ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; } = ReactiveCommand.Create(() => Result.Success(new InstallerMetadata(
        "com.example.app",
        "Example App",
        "1.0.0",
        "Example, Inc.",
        Description: "This is an example app. It does nothing. It's just a demo.",
        HasLogo: true)));

    public ReactiveCommand<Unit, Result<Maybe<IByteSource>>> LoadLogo { get; }

    public ReadOnlyReactivePropertySlim<IBitmap?> Logo { get; }
}
