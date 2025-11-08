using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.Exe.Installer;

public sealed class OptionsPageVM : ReactiveObject, IDisposable
{
    private string installDirectory;
    private readonly Subject<Progress> progressUpdates = new();
    private readonly CompositeDisposable disposables = new();

    public OptionsPageVM(string initialDir, InstallerMetadata metadata)
    {
        installDirectory = initialDir;
        Metadata = metadata;

        Progress = new Reactive.Bindings.ReactiveProperty<Progress?>(progressUpdates.Select(progress => (Progress?)progress).ObserveOn(RxApp.MainThreadScheduler), (Progress?)null).DisposeWith(disposables);
    }

    public InstallerMetadata Metadata { get; }

    public string InstallDirectory
    {
        get => installDirectory;
        set => this.RaiseAndSetIfChanged(ref installDirectory, value);
    }

    public Reactive.Bindings.ReactiveProperty<Progress?> Progress { get; }

    public IObserver<Progress> ProgressObserver => progressUpdates;

    public string ProgressText => Progress.Value switch
    {
        AbsoluteProgress<RelativeProgress<long>> absolute =>
            $"Copied {absolute.Value.Value:N0} / {absolute.Value.Total:N0} bytes ({absolute.Value.Proportion:P0})",
        _ => "Ready to install"
    };

    public double ProgressFraction => Progress.Value switch
    {
        AbsoluteProgress<RelativeProgress<long>> absolute => absolute.Value.Proportion,
        _ => 0
    };

    public bool IsProgressVisible => Progress.Value is not null;
    
    public void Dispose()
    {
        disposables.Dispose();
    }
}
