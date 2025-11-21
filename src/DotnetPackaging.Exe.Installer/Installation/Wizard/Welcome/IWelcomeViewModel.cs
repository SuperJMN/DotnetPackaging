using System.Reactive;
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using Reactive.Bindings;
using ReactiveUI;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Welcome;

public interface IWelcomeViewModel
{
    Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }
    ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }
    ReactiveCommand<Unit, Result<Maybe<IByteSource>>> LoadLogo { get; }
    ReadOnlyReactivePropertySlim<IBitmap?> Logo { get; }
}