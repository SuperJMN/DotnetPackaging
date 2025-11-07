using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.InstallerStub;

public sealed class OptionsPageVM : ReactiveObject, IDisposable
{
    private string installDirectory;
    private readonly Subject<Progress> progressUpdates = new();
    private readonly CompositeDisposable disposables = new();

    public OptionsPageVM(string initialDir, InstallerMetadata metadata)
    {
        installDirectory = initialDir;
        Metadata = metadata;

        Progress = new ReactiveProperty<Progress?>(
            progressUpdates
                .Select(progress => (Progress?)progress)
                .ObserveOn(RxApp.MainThreadScheduler),
            null);

        var subscription = Progress.Changes
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(ProgressText));
                this.RaisePropertyChanged(nameof(ProgressFraction));
                this.RaisePropertyChanged(nameof(IsProgressVisible));
            });
        disposables.Add(subscription);

        disposables.Add(Progress);
        disposables.Add(progressUpdates);
    }

    public InstallerMetadata Metadata { get; }

    public string InstallDirectory
    {
        get => installDirectory;
        set => this.RaiseAndSetIfChanged(ref installDirectory, value);
    }

    public ReactiveProperty<Progress?> Progress { get; }

    public IObserver<Progress> ProgressObserver => progressUpdates;

    public string ProgressText => Progress.Value switch
    {
        AbsoluteProgress<RelativeProgress<long>> absolute =>
            $"Copiado {absolute.Value.Value:N0} / {absolute.Value.Total:N0} bytes ({absolute.Value.Proportion:P0})",
        _ => "Listo para instalar"
    };

    public double ProgressFraction => Progress.Value switch
    {
        AbsoluteProgress<RelativeProgress<long>> absolute => absolute.Value.Proportion,
        _ => 0
    };

    public bool IsProgressVisible => Progress.Value is not null;

    public void ResetProgress()
    {
        Progress.OnNext(null!);
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
