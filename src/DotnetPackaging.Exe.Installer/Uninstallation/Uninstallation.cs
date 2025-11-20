using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Uninstallation.Wizard;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;

namespace DotnetPackaging.Exe.Installer.Uninstallation;

internal static class Uninstallation
{
    public static async Task Launch()
    {
        var currentApp = App.Current;
        if (currentApp?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return;
        }

        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "dp-uninstaller-debug.txt");
            File.WriteAllText(logPath, "Uninstallation launched.\n");

            var root = CreateRootWindow();
            desktopLifetime.MainWindow = root;
            root.Show();
            File.AppendAllText(logPath, "Root window shown.\n");

            var dialog = new DesktopDialog();
            var payload = ResolvePayload(logPath);
            var wizard = new UninstallWizard(payload).CreateWizard();

            File.AppendAllText(logPath, "Wizard created. Resolving title...\n");
            var title = await ResolveWindowTitle(payload);
            File.AppendAllText(logPath, $"Title resolved: {title}\n");
            
            await wizard.ShowInDialog(dialog, title);
            File.AppendAllText(logPath, "Wizard dialog finished.\n");

            await TryTriggerSelfDestruct(payload);
            File.AppendAllText(logPath, "Self-destruct triggered.\n");

            desktopLifetime.Shutdown();
        }
        catch (Exception ex)
        {
            TryWriteCrashLog(ex);
            throw;
        }
    }

    private static IInstallerPayload ResolvePayload(string logPath)
    {
        var payloadResult = MetadataFilePayload.FromProcessDirectory();
        if (payloadResult.IsSuccess)
        {
            TryAppendLog(logPath, "Using metadata.json from installation directory.\n");
            return payloadResult.Value;
        }

        TryAppendLog(logPath, $"Falling back to bundled payload: {payloadResult.Error}\n");
        return new DefaultInstallerPayload();
    }

    private static async Task TryTriggerSelfDestruct(IInstallerPayload payload)
    {
        var meta = await payload.GetMetadata();
        if (meta.IsFailure)
        {
            return;
        }

        var appId = meta.Value.AppId;
        var installation = InstallationRegistry.Get(appId);
        
        // If installation is missing, it means it was successfully removed (or never existed)
        // Check if we are running from the typical install location to decide if we should self-destruct
        if (installation.IsFailure && Environment.ProcessPath is { } path)
        {
            var directory = Path.GetDirectoryName(path);
            if (directory != null)
            {
                // We schedule self-destruct for the directory where the uninstaller resides
                // This assumes the uninstaller is in the application directory
                SelfDestruct.Schedule(path, directory);
            }
        }
    }

    private static Window CreateRootWindow()
        => new()
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false
        };

    private static async Task<string> ResolveWindowTitle(IInstallerPayload payload)
    {
        var metaResult = await payload.GetMetadata();
        if (metaResult.IsFailure)
        {
             try 
             { 
                 File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-uninstaller-debug.txt"), $"Metadata load failed: {metaResult.Error}\n"); 
             } 
             catch { }
        }
        var title = metaResult.IsSuccess && !string.IsNullOrWhiteSpace(metaResult.Value.ApplicationName)
            ? $"{metaResult.Value.ApplicationName} Uninstaller"
            : "Uninstaller";

        return title;
    }

    private static void TryAppendLog(string logPath, string message)
    {
        try
        {
            File.AppendAllText(logPath, message);
        }
        catch
        {
            // ignored
        }
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
