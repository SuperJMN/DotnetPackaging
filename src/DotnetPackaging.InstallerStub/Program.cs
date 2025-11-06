using Avalonia;
using ReactiveUI.Avalonia;

namespace DotnetPackaging.InstallerStub;

internal static class Program
{
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
#if DEBUG
            // .WithDeveloperTools()
#endif
            .UseReactiveUI();
}