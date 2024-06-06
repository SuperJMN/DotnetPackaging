using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using DotnetPackaging.Gui.Core;
using DotnetPackaging.Gui.ViewModels;
using DotnetPackaging.Gui.Views;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Simple;
using Zafiro.Avalonia.Notifications;
using Zafiro.Avalonia.Storage;

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

        Zafiro.Avalonia.Mixins.ApplicationMixin.Connect(this, () => new PackagerSelectionView(), control =>
        {
            var topLevel = TopLevel.GetTopLevel(control)!;
            var picker = new AvaloniaFileSystemPicker(topLevel.StorageProvider);

            var notificationService = new NotificationService(new WindowNotificationManager(topLevel));
            var packagers = new IPackager[] { new AppImagePackager(), new DebImagePackager() };
            var simpleDesktopDialogService = new SimpleDesktopDialogService(Maybe<Action<ConfigureWindowContext>>.None);
            var main = new PackagerSelectionViewModel(packagers, packager => new PackageViewModel(packager, picker, notificationService, simpleDesktopDialogService));
            return main;
        }, () => new MainWindow());
    }
}
