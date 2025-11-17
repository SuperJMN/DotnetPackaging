using System.Reactive;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Welcome;

public interface IWelcomeViewModel
{
    Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }
    ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }
}