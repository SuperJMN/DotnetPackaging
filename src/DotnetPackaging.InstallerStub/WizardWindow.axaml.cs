using Avalonia.Controls;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace DotnetPackaging.InstallerStub;

public sealed partial class WizardWindow : Window
{
    public WizardWindow()
    {
        InitializeComponent();
        DataContext = new WizardViewModel();
    }
}

public sealed class WizardViewModel : ReactiveObject
{
    private readonly string contentDir;
    private readonly InstallerMetadata metadata;

    private string installDirectory;
    private string status = "Ready";

    public string ApplicationName => metadata.ApplicationName;

    public string InstallDirectory
    {
        get => installDirectory;
        set => this.RaiseAndSetIfChanged(ref installDirectory, value);
    }

    public string Status
    {
        get => status;
        set => this.RaiseAndSetIfChanged(ref status, value);
    }

    public ReactiveCommand<Unit, Unit> InstallCommand { get; }

    public WizardViewModel()
    {
        // Extract payload and prefill defaults
        try
        {
            var extracted = PayloadExtractor.Extract();
            contentDir = extracted.contentDir;
            metadata = extracted.meta;

            var baseDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs");
            var vendorPart = SanitizePathPart(metadata.Vendor);
            var appPart = SanitizePathPart(metadata.ApplicationName);
            // Avoid duplicated segment when vendor equals app
            string defaultDir = string.Equals(vendorPart, appPart, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(vendorPart)
                ? System.IO.Path.Combine(baseDir, appPart)
                : System.IO.Path.Combine(baseDir, vendorPart, appPart);

            installDirectory = defaultDir;
        }
        catch (Exception ex)
        {
            metadata = new InstallerMetadata("app", "App", "1.0.0", "Unknown");
            contentDir = string.Empty;
            installDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "App");
            status = $"Error reading payload: {ex.Message}";
        }

        InstallCommand = ReactiveCommand.CreateFromTask(Install);
    }

    private async Task Install()
    {
        try
        {
            Status = "Installing...";
            await Task.Run(() => Installer.Install(contentDir, InstallDirectory, metadata));
            Status = "Installed";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    private static string SanitizePathPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "App";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "App" : sanitized;
    }
}
