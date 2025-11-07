using ReactiveUI;

namespace DotnetPackaging.InstallerStub;

public sealed class OptionsPageVM : ReactiveObject
{
    private string installDirectory;

    public OptionsPageVM(string initialDir, InstallerMetadata metadata)
    {
        installDirectory = initialDir;
        Metadata = metadata;
    }

    public InstallerMetadata Metadata { get; }

    public string InstallDirectory
    {
        get => installDirectory;
        set => this.RaiseAndSetIfChanged(ref installDirectory, value);
    }
}
