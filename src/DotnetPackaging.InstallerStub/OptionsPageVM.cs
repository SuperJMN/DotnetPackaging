using ReactiveUI;

namespace DotnetPackaging.InstallerStub;

public sealed class OptionsPageVM : ReactiveObject
{
    private string installDirectory;
    public OptionsPageVM(string initialDir) { installDirectory = initialDir; }
    public string InstallDirectory
    {
        get => installDirectory;
        set => this.RaiseAndSetIfChanged(ref installDirectory, value);
    }
}