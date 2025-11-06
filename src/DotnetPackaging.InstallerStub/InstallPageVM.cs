using ReactiveUI;

namespace DotnetPackaging.InstallerStub;

public sealed class InstallPageVM : ReactiveObject
{
    public InstallPageVM(InstallerMetadata meta, string contentDir, string targetDir)
    {
        Metadata = meta;
        ContentDir = contentDir;
        TargetDir = targetDir;
    }

    public InstallerMetadata Metadata { get; }
    public string ContentDir { get; }
    public string TargetDir { get; }

    private string status = "Ready";
    public string Status
    {
        get => status;
        set => this.RaiseAndSetIfChanged(ref status, value);
    }
}