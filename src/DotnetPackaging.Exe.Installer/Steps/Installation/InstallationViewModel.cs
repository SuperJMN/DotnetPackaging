using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Steps.Installation;

public class InstallationViewModel 
{
    public InstallationViewModel(InstallerMetadata installerMetadata, string installDirectory)
    {
        Install = ReactiveCommand.CreateFromTask(() => DoInstall().Map(() => installerMetadata)).Enhance();
    }

    private async Task<Result> DoInstall()
    {
        await Task.Delay(2000);
        return await Task.FromResult(Result.Success());
    }

    public IEnhancedCommand<Result<InstallerMetadata>> Install { get; }
}