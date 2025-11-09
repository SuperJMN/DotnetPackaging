using System.Reactive;
using CSharpFunctionalExtensions;
using ReactiveUI;

namespace DotnetPackaging.Exe.Installer.Steps.Welcome;

public interface IWelcomeViewModel
{
    Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }
    ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }
}