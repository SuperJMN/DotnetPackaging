using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Uninstallation.Wizard.Steps;

public class UninstallViewModel
{
    private readonly InstallerMetadata metadata;
    private readonly RegisteredInstallation? installation;

    public UninstallViewModel(InstallerMetadata metadata)
    {
        this.metadata = metadata;
        var installationResult = InstallationRegistry.Get(metadata.AppId);
        if (installationResult.IsSuccess)
        {
            installation = installationResult.Value;
        }
        else
        {
            ErrorMessage = installationResult.Error;
        }

        CanUninstall = installationResult.IsSuccess;
        var canExecute = Observable.Return(CanUninstall);
        Uninstall = ReactiveCommand.CreateFromTask(ExecuteUninstall, canExecute).Enhance();
    }

    public string? InstallDirectory => installation?.InstallDirectory;

    public string? ErrorMessage { get; }

    public string ApplicationName => metadata.ApplicationName;

    public bool CanUninstall { get; }

    public bool InstallationMissing => !CanUninstall;

    public IEnhancedCommand<Result<UninstallationResult>> Uninstall { get; }

    private Task<Result<UninstallationResult>> ExecuteUninstall()
    {
        return Task.Run(() =>
        {
            if (installation is null)
            {
                return Result.Failure<UninstallationResult>(ErrorMessage ?? "Installation not found.");
            }

            return Uninstaller.Uninstall(metadata, installation);
        });
    }
}
