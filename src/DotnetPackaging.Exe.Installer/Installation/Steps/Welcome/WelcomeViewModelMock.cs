using System.Reactive;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;

namespace DotnetPackaging.Exe.Installer.Steps.Welcome;

public class WelcomeViewModelMock : IWelcomeViewModel
{
    public WelcomeViewModelMock()
    {
        Metadata = new Reactive.Bindings.ReactiveProperty<InstallerMetadata>(new InstallerMetadata(
            "com.example.app",
            "Example App",
            "1.0.0",
            "Example, Inc.", Description: "This is an example app. It does nothing. It's just a demo."));
    }

    public Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }

    public ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }
}