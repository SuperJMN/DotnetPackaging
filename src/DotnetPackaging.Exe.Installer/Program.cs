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
                var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-installer-stub-error.txt");
                System.IO.File.WriteAllText(log, ex.ToString());
            }
            catch
            {
                // ignored
            }

            throw;
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