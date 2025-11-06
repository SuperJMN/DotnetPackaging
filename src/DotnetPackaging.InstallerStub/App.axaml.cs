using Avalonia;
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
using Zafiro.Avalonia.Misc;
using Zafiro.UI.Navigation;

namespace DotnetPackaging.InstallerStub;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();
        
        void Shutdown()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        this.Connect(() => new MainView(), content =>
        {
            var buildServiceProvider = new ServiceCollection().BuildServiceProvider();
            return new WizardViewModel(new DesktopDialog(), new Navigator(buildServiceProvider, Maybe<ILogger>.None, RxApp.MainThreadScheduler), Shutdown);
        }, () => new WizardWindow());
        
        base.OnFrameworkInitializationCompleted();
    }
}
