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
        
       

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
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
            var folderPicker = new AvaloniaFolderPickerService(root.StorageProvider);
            var wizard = new InstallWizard(folderPicker).CreateWizard();
        
            await wizard.ShowInDialog(dialog, "Installer");

            lifetime.Shutdown();
        }
    }
}
