using System.Reactive;
using Avalonia;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Serilog;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace DotnetPackaging.InstallerStub;

public sealed class WizardViewModel : ReactiveObject
{
    private readonly InstallerMetadata metadata;
    private readonly Result<PayloadExtractor.PayloadPreparation> payloadPreparation;

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

    public INavigator Navigator { get; }

    public WizardViewModel(IDialog dialog, INavigator navigator, Action onCancel)
    {
        Navigator = navigator;
        
        payloadPreparation = PayloadExtractor.Prepare();

        if (payloadPreparation.IsSuccess)
        {
            metadata = payloadPreparation.Value.Metadata;
            var baseDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs");
            var vendorPart = SanitizePathPart(metadata.Vendor);
            var appPart = SanitizePathPart(metadata.ApplicationName);
            string defaultDir = string.Equals(vendorPart, appPart, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(vendorPart)
                ? System.IO.Path.Combine(baseDir, appPart)
                : System.IO.Path.Combine(baseDir, vendorPart, appPart);

            installDirectory = defaultDir;
        }
        else
        {
            metadata = new InstallerMetadata("app", "App", "1.0.0", "Unknown");
            installDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "App");
            status = $"Error reading payload: {payloadPreparation.Error}";
        }

        // Build wizard
        var welcome = new WelcomePageVM(metadata);
        var options = new OptionsPageVM(installDirectory);
        var wizard = CreateWizard(welcome, options);
        LoadWizard = ReactiveCommand.CreateFromTask(() => wizard.Navigate(Navigator, async (slimWizard, navigator) =>
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
        }));

        LoadWizard.Execute().Subscribe();
    }

    public ReactiveCommand<Unit, Maybe<string>> LoadWizard { get; }

    private SlimWizard<string> CreateWizard(WelcomePageVM welcome, OptionsPageVM options)
    {
        return WizardBuilder
            .StartWith(() => welcome, "Welcome")
            .ProceedWith(_ => EnhancedCommand.Create(() => Result.Success(Unit.Default), text: "Next"))
            .Then(_ => options, "Destination")
            .ProceedWith(page => EnhancedCommand.Create(() => PrepareInstallation(page.InstallDirectory), text: "Install"))
            .Then(context => new InstallPageVM(metadata, context.ContentDirectory, context.InstallDirectory), "Install")
            .ProceedWith((vm, _) => EnhancedCommand.Create(() => ExecuteInstall(vm, metadata)))
            .WithCompletionFinalStep();
    }

    private Result<InstallationContext> PrepareInstallation(string installDir)
    {
        return payloadPreparation
            .Bind(preparation => preparation.ExtractContent()
                .Map(contentDir => new InstallationContext(installDir, contentDir)));
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

    private static string SanitizePathPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "App";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "App" : sanitized;
    }

    private sealed record InstallationContext(string InstallDirectory, string ContentDirectory);
}
