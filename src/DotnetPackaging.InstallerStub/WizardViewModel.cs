using System.Reactive;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace DotnetPackaging.InstallerStub;

public sealed class WizardViewModel : ReactiveObject
{
    private InstallerMetadata metadata = new("app", "App", "1.0.0", "Unknown");

    public string ApplicationName => metadata.ApplicationName;

    public INavigator Navigator { get; }

    public WizardViewModel(IDialog dialog, INavigator navigator, Action onCancel)
    {
        Navigator = navigator;

        LoadWizard = ReactiveCommand.CreateFromTask<InstallerPayload, Maybe<string>>(payload =>
        {
            var wizard = CreateWizard(payload);
            return wizard.Navigate(Navigator, async (_, _) =>
            {
                var result = await dialog.ShowConfirmation("Cancel Installation", "Are you sure you want to cancel the installation?");
                result.Tap(b =>
                {
                    if (b)
                    {
                        onCancel();
                    }
                });

                return result.GetValueOrDefault(false);
            });
        });

        LoadPayload = ReactiveCommand.Create(() =>
        {
            var payloadResult = PayloadExtractor.LoadPayload();

            if (payloadResult.IsSuccess)
            {
                var payload = payloadResult.Value;
                metadata = payload.Metadata;
                this.RaisePropertyChanged(nameof(ApplicationName));
            }
            else
            {
                metadata = new InstallerMetadata("app", "App", "1.0.0", "Unknown");
                this.RaisePropertyChanged(nameof(ApplicationName));
            }

            return payloadResult;
        });

        LoadPayload.Execute().Subscribe();
        LoadPayload.Successes().InvokeCommand(LoadWizard);
    }

    public ReactiveCommand<Unit, Result<InstallerPayload>> LoadPayload { get; }

    public ReactiveCommand<InstallerPayload, Maybe<string>> LoadWizard { get; }

    private SlimWizard<string> CreateWizard(InstallerPayload payload)
    {
        var welcome = new WelcomePageVM(payload.Metadata);
        var options = new OptionsPageVM("C:\\");

        return WizardBuilder
            .StartWith(() => welcome, "Welcome").NextUnit().Always()
            .Then(_ => options, "Destination").NextCommand(page => EnhancedCommand.Create(() => PrepareInstallation(payload, page.InstallDirectory), text: "Install"))
            .Then(context => new InstallPageVM(payload.Metadata, context.ContentDirectory, context.InstallDirectory), "Install").NextCommand((vm, _) => EnhancedCommand.Create(() => ExecuteInstall(vm, payload.Metadata)))
            .WithCompletionFinalStep();
    }

    private Result<InstallationContext> PrepareInstallation(InstallerPayload payload, string installDir)
    {
        return PayloadExtractor.ExtractContent(payload)
            .Map(contentDir => new InstallationContext(installDir, contentDir));
    }

    private static Result<string> ExecuteInstall(InstallPageVM vm, InstallerMetadata metadata)
    {
        try
        {
            vm.Status = "Installing...";
            Installer.Install(vm.ContentDir, vm.TargetDir, metadata);
            vm.Status = "Installed";
            return Result.Success("OK");
        }
        catch (Exception ex)
        {
            vm.Status = $"Failed: {ex.Message}";
            return Result.Failure<string>(ex.Message);
        }
    }

    private static string GetDefaultInstallDirectory(InstallerMetadata installerMetadata)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        var vendorPart = SanitizePathPart(installerMetadata.Vendor);
        var appPart = SanitizePathPart(installerMetadata.ApplicationName);

        return string.Equals(vendorPart, appPart, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(vendorPart)
            ? Path.Combine(baseDir, appPart)
            : Path.Combine(baseDir, vendorPart, appPart);
    }

    private static string SanitizePathPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "App";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "App" : sanitized;
    }

    private sealed record InstallationContext(string InstallDirectory, string ContentDirectory);
}
