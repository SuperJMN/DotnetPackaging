using System.Reactive.Linq;
using System.Reactive.Subjects;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.ProgressReporting;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Steps.Install;

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
            var copyRes = await payload.CopyContents(installDirectory, progress).ConfigureAwait(false);
            if (copyRes.IsFailure)
            {
                return Result.Failure<InstallationResult>(copyRes.Error);
            }

            return Installer.Install(installDirectory, installerMetadata)
                .Map(exePath => new InstallationResult(installerMetadata, installDirectory, exePath));
        });
    }

    public IObservable<Progress> Progress => progress.AsObservable();

    public IEnhancedCommand<Result<InstallationResult>> Install { get; }
}
