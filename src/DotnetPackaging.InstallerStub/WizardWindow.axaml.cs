using Avalonia.Controls;
using ReactiveUI;
using CSharpFunctionalExtensions;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

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

    public ISlimWizard<string> Wizard { get; }

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

        // Build wizard
        var options = new OptionsPageVM(installDirectory);
        Wizard = WizardBuilder
            .StartWith(() => options, "Destination")
            .Next(page => page.InstallDirectory)
            .Always()
            .Then(dir => new InstallPageVM(metadata, contentDir, dir), "Install")
            .NextResult((vm, selectedDir) => ExecuteInstall(vm, metadata, selectedDir))
            .Always()
            .WithCompletionFinalStep();
    }

    private static Result<string> ExecuteInstall(InstallPageVM vm, InstallerMetadata metadata, string targetDirectory)
    {
        try
        {
            vm.Status = "Installing...";
            Installer.Install(vm.ContentDir, targetDirectory, metadata);
            vm.Status = "Installed";
            return Result.Success("OK");
        }
        catch (Exception ex)
        {
            vm.Status = $"Failed: {ex.Message}";
            return Result.Failure<string>(ex.Message);
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
