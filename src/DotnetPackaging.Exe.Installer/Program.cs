using Avalonia;
using ReactiveUI.Avalonia;

namespace DotnetPackaging.Exe.Installer;

internal static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "dp-installer-crash.txt");
                File.WriteAllText(logPath, ex.ToString());
            }
            catch
            {
                // Ignore
            }
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
