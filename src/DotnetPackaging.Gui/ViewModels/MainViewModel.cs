using System;
using System.Collections.Generic;
using System.Reactive;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.Avalonia.Storage;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem.Mutable;

namespace DotnetPackaging.Gui.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AvaloniaFilePicker picker;

    public MainViewModel(AvaloniaFilePicker picker)
    {
        this.picker = picker;
        SelectFolder = ReactiveCommand.CreateFromTask(picker.PickFolder);
        SelectFolder.Values().Subscribe(async enumerable =>
        {
            foreach (var mutableDirectory in enumerable)
            {
                var children = await mutableDirectory.MutableChildren();
                foreach (var mutableNode in children.Value)
                {
                }
            }
        });
    }

    public ReactiveCommand<Unit, Maybe<IEnumerable<IMutableDirectory>>> SelectFolder { get; set; }

    public string Greeting => "Welcome to Avalonia!";
}
