﻿using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
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
        
        Zafiro.Avalonia.Mixins.ApplicationMixin.Connect(this, () => new MainView(), control =>
        {
            var topLevel = TopLevel.GetTopLevel(control)!;
            var picker = new AvaloniaFileSystemPicker(topLevel.StorageProvider);
            return new MainViewModel(picker, new NotificationService(new WindowNotificationManager(topLevel)), new SimpleDesktopDialogService(Maybe<Action<ConfigureWindowContext>>.None));
        }, () => new MainWindow());
    }
}
