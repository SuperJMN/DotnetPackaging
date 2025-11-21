using Avalonia.Controls.ApplicationLifetimes;
using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Flows.Installation.Wizard;
using Serilog;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe.Installer.Flows.Installation;

internal static class Installer
{
    public static async Task Launch()
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            Log.Warning("Application lifetime is not classic desktop");
            return;
        }

        if (desktopLifetime.MainWindow is null)
        {
            Log.Error("MainWindow not set");
            return;
        }

        try
        {
            Log.Information("Installation launched");

            var dialog = new DesktopDialog();
            var folderPicker = new AvaloniaFolderPickerService(desktopLifetime.MainWindow.StorageProvider);
            var payload = new DefaultInstallerPayload();
            var wizard = new InstallWizard(folderPicker, payload).CreateWizard();

            Log.Information("Wizard created, resolving title");
            var title = await ResolveWindowTitle(payload);
            Log.Information("Title resolved: {Title}", title);

            await wizard.ShowInDialog(dialog, title);
            Log.Information("Wizard dialog finished");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Installation failed");
            TryWriteCrashLog(ex);
            throw;
        }
    }


    private static async Task<string> ResolveWindowTitle(DefaultInstallerPayload payload)
    {
        var metaResult = await payload.GetMetadata();
        var title = metaResult.IsSuccess && !string.IsNullOrWhiteSpace(metaResult.Value.ApplicationName)
            ? $"{metaResult.Value.ApplicationName} Installer"
            : "Installer";

        return title;
    }

    private static void TryWriteCrashLog(Exception ex)
    {
        try
        {
            var log = Path.Combine(Path.GetTempPath(), "dp-installer-stub-error.txt");
            File.WriteAllText(log, ex.ToString());
        }
        catch
        {
            // ignored
        }
    }
}