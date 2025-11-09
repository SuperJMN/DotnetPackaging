using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Projektanker.Icons.Avalonia.MaterialDesign;
using ReactiveUI;
using Serilog;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.Avalonia.Misc;
using Zafiro.UI;
using Zafiro.UI.Navigation;

namespace DotnetPackaging.Exe.Installer;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();
        
        var lifetime = (IClassicDesktopStyleApplicationLifetime) ApplicationLifetime!;
        
        var root = new Window
        {
            Width = 1000,
            Height = 1000,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false
        };

        lifetime.MainWindow = root;
        root.Show();
        
        var dialog = new DesktopDialog();
        var notificationService = new NotificationDialog(dialog);
        var buildServiceProvider = new ServiceCollection().BuildServiceProvider();
        var navigator = new Navigator(buildServiceProvider, Maybe<ILogger>.None, RxApp.MainThreadScheduler);
        var wizard =new InstallWizard().CreateWizard();
        
        await wizard.ShowInDialog(dialog, "notificationService");

        lifetime.Shutdown();
        // return;
        //
        // void Shutdown()
        // {
        //     if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        //     {
        //         desktop.Shutdown();
        //     }
        // }
        //
        // this.Connect(() => new MainView(), content =>
        // {
        //     var dialog = new DesktopDialog();
        //     var notificationService = new NotificationDialog(dialog);
        //     var buildServiceProvider = new ServiceCollection().BuildServiceProvider();
        //     var navigator = new Navigator(buildServiceProvider, Maybe<ILogger>.None, RxApp.MainThreadScheduler);
        //     return new MainViewModel(new InstallWizard(), dialog, navigator, notificationService, Shutdown);
        // }, () => new WizardWindow());
        //
        base.OnFrameworkInitializationCompleted();
    }
}
