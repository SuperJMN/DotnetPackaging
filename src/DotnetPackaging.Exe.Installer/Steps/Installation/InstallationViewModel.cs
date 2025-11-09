using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.ProgressReporting;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Steps.Installation;

public class InstallationViewModel 
{
    public InstallationViewModel(InstallerMetadata installerMetadata, string installDirectory)
    {
        Install = ReactiveCommand.CreateFromTask(() => DoInstall(installDirectory).Map(() => installerMetadata)).Enhance();
    }

    private async Task<Result> DoInstall(string installDirectory)
    {
        await Task.Delay(2000);
        return await Task.FromResult(Result.Success());
    }

    public IObservable<Progress> Progress { get; } = Observable.Return(Unknown.Instance);

    public IEnhancedCommand<Result<InstallerMetadata>> Install { get; }
}