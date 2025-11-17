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
            var root = CreateRootWindow();
            desktopLifetime.MainWindow = root;
            root.Show();

            var dialog = new DesktopDialog();
            var payload = new DefaultInstallerPayload();
            var wizard = new UninstallWizard(payload).CreateWizard();

            var title = await ResolveWindowTitle(payload);
            await wizard.ShowInDialog(dialog, title);

            desktopLifetime.Shutdown();
        }
        catch (Exception ex)
        {
            TryWriteCrashLog(ex);
            throw;
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

    private static async Task<string> ResolveWindowTitle(DefaultInstallerPayload payload)
    {
        var metaResult = await payload.GetMetadata();
        var title = metaResult.IsSuccess && !string.IsNullOrWhiteSpace(metaResult.Value.ApplicationName)
            ? $"{metaResult.Value.ApplicationName} Uninstaller"
            : "Uninstaller";

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
