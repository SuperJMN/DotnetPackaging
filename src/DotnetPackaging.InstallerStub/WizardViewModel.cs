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

    public INavigator Navigator { get; }

    public WizardViewModel(IDialog dialog, INavigator navigator, Action onCancel)
    {
        Navigator = navigator;
        
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
            .ProceedWith(page => EnhancedCommand.Create(() => Result.Success(page.InstallDirectory), text: "Install"))
            .Then(dir => new InstallPageVM(metadata, contentDir, dir), "Install")
            .ProceedWith((vm, selectedDir) => EnhancedCommand.Create(() => ExecuteInstall(vm, metadata, selectedDir)))
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