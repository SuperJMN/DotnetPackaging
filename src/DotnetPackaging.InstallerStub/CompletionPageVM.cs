using System.Diagnostics;
using System.Reactive;
using ReactiveUI;

namespace DotnetPackaging.InstallerStub;

public sealed class CompletionPageVM : ReactiveObject
{
    private string status;

    public CompletionPageVM(InstallationResult result)
    {
        Metadata = result.Metadata;
        InstallDirectory = result.InstallDirectory;
        ExecutablePath = result.ExecutablePath;
        status = $"Instalación completada en {InstallDirectory}.";
        LaunchCommand = ReactiveCommand.Create(LaunchApplication);
    }

    public InstallerMetadata Metadata { get; }

    public string InstallDirectory { get; }

    public string ExecutablePath { get; }

    public ReactiveCommand<Unit, Unit> LaunchCommand { get; }

    public string Status
    {
        get => status;
        private set => this.RaiseAndSetIfChanged(ref status, value);
    }

    private void LaunchApplication()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(ExecutablePath) ?? InstallDirectory,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            Status = "Aplicación iniciada.";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo iniciar la aplicación: {ex.Message}";
        }
    }
}
