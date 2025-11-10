using System.Reactive;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;

namespace DotnetPackaging.Exe.Installer.Steps.Welcome;

public class WelcomeViewModelMock : IWelcomeViewModel
{
    private Reactive.Bindings.ReactiveProperty<InstallerMetadata?> metadata;

    public InstallerMetadata Metadata { get; } = new InstallerMetadata(
        "com.example.app",
        "Example App",
        "1.0.0",
        "Example, Inc.", Description: "");

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }

    Reactive.Bindings.ReactiveProperty<InstallerMetadata?> IWelcomeViewModel.Metadata => metadata;
}