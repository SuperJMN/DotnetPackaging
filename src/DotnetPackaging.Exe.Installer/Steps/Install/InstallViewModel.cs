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

    public InstallViewModel(InstallerMetadata installerMetadata, string installDirectory)
    {
        // Emit initial unknown progress for the UI
        progress.OnNext(Unknown.Instance);

        Install = ReactiveCommand.CreateFromTask(() => DoInstall(installerMetadata, installDirectory)).Enhance();
    }

    private Task<Result<InstallationResult>> DoInstall(InstallerMetadata installerMetadata, string installDirectory)
    {
        // Load payload and copy its Content/ tree into the chosen install directory, reporting progress
        var payloadResult = PayloadExtractor.LoadPayload();
        if (payloadResult.IsFailure)
        {
            return Task.FromResult(Result.Failure<InstallationResult>(payloadResult.Error));
        }

        var payload = payloadResult.Value;
        return Task.Run(() =>
            PayloadExtractor.CopyContentTo(payload, installDirectory, progress)
                .Bind(() => Installer.Install(installDirectory, installerMetadata))
                .Map(exePath => new InstallationResult(installerMetadata, installDirectory, exePath))
        );
    }

    public IObservable<Progress> Progress => progress.AsObservable();

    public IEnhancedCommand<Result<InstallationResult>> Install { get; }
}
