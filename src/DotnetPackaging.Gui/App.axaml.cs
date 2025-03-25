using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using DotnetPackaging.Gui.Core;
using DotnetPackaging.Gui.ViewModels;
using DotnetPackaging.Gui.Views;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Mixins;
using Zafiro.Avalonia.Services;
using Zafiro.Avalonia.Storage;

namespace DotnetPackaging.Gui;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        this.Connect(() => new PackagerSelectionView(),control =>
        {
            var topLevel = TopLevel.GetTopLevel(control)!;
            var picker = new AvaloniaFileSystemPicker(topLevel.StorageProvider);

            var notificationService = new NotificationService(new WindowNotificationManager(topLevel));
            var packagers = new IPackager[] { new AppImagePackager(), new DebImagePackager() };
            var dialogService = DialogService.Create();
            var options = new OptionsViewModel(picker);
            var main = new PackagerSelectionViewModel(packagers, packager => new PackageViewModel(packager, options, picker, notificationService, dialogService));
            return main;
        });
    }
}
