using System.Reactive.Linq;
using System.Reactive.Subjects;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;
using Serilog;
using Zafiro.ProgressReporting;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Install;

public class InstallViewModel 
{
    private readonly ISubject<Progress> progress = new ReplaySubject<Progress>(1);
    private readonly IInstallerPayload payload;

    public InstallViewModel(IInstallerPayload payload, InstallerMetadata installerMetadata, string installDirectory)
    {
        this.payload = payload;
        // Emit initial unknown progress for the UI
        progress.OnNext(Unknown.Instance);

        Install = ReactiveCommand.CreateFromTask(() => DoInstall(installerMetadata, installDirectory)).Enhance();
    }

    private Task<Result<InstallationResult>> DoInstall(InstallerMetadata installerMetadata, string installDirectory)
    {
        return Task.Run(async () =>
        {
            Log.Information("Starting installation to {InstallDirectory}", installDirectory);
            
            var copyRes = await payload.CopyContents(installDirectory, progress).ConfigureAwait(false);
            if (copyRes.IsFailure)
            {
                Log.Error("Failed to copy contents: {Error}", copyRes.Error);
                return Result.Failure<InstallationResult>(copyRes.Error);
            }

            Log.Information("Contents copied successfully, registering installation");

            var installResult = Core.Installer.Install(installDirectory, installerMetadata)
                .Map(exePath => new InstallationResult(installerMetadata, installDirectory, exePath))
                .Bind(result => InstallationRegistry.Register(result).Map(() => result));

            if (installResult.IsFailure)
            {
                Log.Error("Installation failed: {Error}", installResult.Error);
            }
            else
            {
                Log.Information("Installation completed successfully");
            }

            return installResult;
        });
    }

    public IObservable<Progress> Progress => progress.AsObservable();

    public IEnhancedCommand<Result<InstallationResult>> Install { get; }
}
