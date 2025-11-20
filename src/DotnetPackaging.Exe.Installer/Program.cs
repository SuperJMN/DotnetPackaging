using Avalonia;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI.Avalonia;
using Serilog;

namespace DotnetPackaging.Exe.Installer;

internal static class Program
{
    public static void Main(string[] args)
    {
        // Emergency early log before anything else
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-emergency.log"), 
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} Program.Main started with args: [{string.Join(", ", args)}]\n");
        }
        catch { /* absolute best effort */ }

        var isUninstaller = args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase));
        LoggerSetup.ConfigureLogger(isUninstaller);

        try
        {
            Log.Information("Starting with args: {Args}", args);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, shutdownMode: Avalonia.Controls.ShutdownMode.OnMainWindowClose);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application crashed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .UseReactiveUI();
}
