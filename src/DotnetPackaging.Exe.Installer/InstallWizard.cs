using System.Reactive;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Installer.Steps;
using DotnetPackaging.Exe.Installer.Steps.Finish;
using DotnetPackaging.Exe.Installer.Steps.Installation;
using DotnetPackaging.Exe.Installer.Steps.Welcome;
using Zafiro.ProgressReporting;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace DotnetPackaging.Exe.Installer;

public class InstallWizard
{
    public InstallWizard()
    {
        
    }
    
    public SlimWizard<Unit> CreateWizard()
    {
        var welcome = new WelcomeViewModel();

        return WizardBuilder
            .StartWith(() => welcome, "").Next(w => w.Metadata.Value).WhenValid()
            .Then(md => new Steps.Location.LocationViewModel(), "Destination").Next((vm, m) => new { vm.InstallDirectory, m }).WhenValid()
            .Then(s => new InstallationViewModel(s.m, s.InstallDirectory!), "Ready to install").NextCommand(model => model.Install.Enhance("Install"))
            .Then(m => new FinishViewModel(m), "Installation finished").NextUnit("Close").Always()
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
                .Bind(() => Installer.Install(installDir, payload.Metadata))
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