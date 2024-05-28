using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using DotnetPackaging.Gui.ViewModels;
using DotnetPackaging.Gui.Views;
using Zafiro.Avalonia.Storage;
using Zafiro.FileSystem;

namespace DotnetPackaging.Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        
        Zafiro.Avalonia.Mixins.ApplicationMixin.Connect(this, () => new MainView(), control =>
        {
            var picker = new AvaloniaFilePicker(TopLevel.GetTopLevel(control)!.StorageProvider);
            return new MainViewModel(picker);
        }, () => new MainWindow());
    }
}
