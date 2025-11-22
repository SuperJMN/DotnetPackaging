using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Finish;
using DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Install;
using DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Location;
using Zafiro.ProgressReporting;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace DotnetPackaging.Exe.Installer.Flows.Installation.Wizard;

public class InstallWizard
{
    private readonly IFolderPickerService folderPicker;
    private readonly IInstallerPayload payload;

    public InstallWizard(IFolderPickerService folderPicker, IInstallerPayload payload)
    {
        this.folderPicker = folderPicker;
        this.payload = payload;
    }
    
    public SlimWizard<InstallationResult> CreateWizard()
    {
        var welcome = new WelcomeViewModel(payload);

        return WizardBuilder
            .StartWith(() => welcome, "").Next(w => w.Metadata.Value).WhenValid()
            .Then(md => new LocationViewModel(folderPicker, GetDefaultInstallDirectory(md)), "Location").Next((vm, m) => new { vm.InstallDirectory, m }).WhenValid()
            .Then(s => new InstallViewModel(payload, s.m, s.InstallDirectory!), "Ready to install").NextCommand(model => model.Install.Enhance("Install"))
            .Then(m => new FinishViewModel(m), "Installation finished").NextCommand(vm => vm.Close.Enhance("Close"))
            .WithCompletionFinalStep();
    }

    private static string GetDefaultInstallDirectory(InstallerMetadata installerMetadata)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        var vendorPart = SanitizePathPart(installerMetadata.Vendor);
        var appPart = SanitizePathPart(installerMetadata.ApplicationName);

        return string.Equals(vendorPart, appPart, StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(vendorPart)
            ? Path.Combine(baseDir, appPart)
            : Path.Combine(baseDir, vendorPart, appPart);
    }
    
    private static Task<Result<InstallationResult>> InstallApplicationAsync(InstallerPayload payload, string installDir,
        IObserver<Progress> progressObserver)
    {
        return Task.Run(() =>
            PayloadExtractor.CopyContentTo(payload, installDir, progressObserver)
                .Bind(() => Core.Installer.Install(installDir, payload.Metadata, payload.ContentSizeBytes, payload.Logo))
                .Map(exePath => new InstallationResult(payload.Metadata, installDir, exePath)));
    }
    
    private static string SanitizePathPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "App";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "App" : sanitized;
    }

}
