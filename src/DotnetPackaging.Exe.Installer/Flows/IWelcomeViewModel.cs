using System.Reactive;
using Avalonia.Media;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using Reactive.Bindings;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Flows;

public interface IWelcomeViewModel
{
    Reactive.Bindings.ReactiveProperty<InstallerMetadata?> Metadata { get; }
    ReactiveUI.ReactiveCommand<Unit, Result<InstallerMetadata>> LoadMetadata { get; }
    ReactiveUI.ReactiveCommand<Unit, Result<Maybe<IByteSource>>> LoadLogo { get; }
    ReadOnlyReactivePropertySlim<IImage?> Logo { get; }
    ReadOnlyReactivePropertySlim<string> Title { get; }
    string OperationName { get; }
    string Preposition { get; }
}
