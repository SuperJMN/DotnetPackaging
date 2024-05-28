using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Zafiro.Avalonia.Storage;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Mutable;

namespace DotnetPackaging.Gui.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AvaloniaFilePicker picker;
    private readonly ObservableAsPropertyHelper<IDirectory> directory;
    private readonly ObservableAsPropertyHelper<IMutableFile> file;

    public MainViewModel(AvaloniaFilePicker picker)
    {
        this.picker = picker;
        SelectDirectory = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(picker.PickFolder).Values().SelectMany(x => x.ToLightweight()).Successes());
        SelectFile = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(() => picker.PickForSave("Package", ".appImage")).Values());
        
        directory = SelectDirectory.ToProperty(this, x => x.Directory);
        file = SelectFile.ToProperty(this, x => x.File);
        CreatePackage = ReactiveCommand.Create(() => { }, this.WhenAnyValue(x => x.Directory, x => x.File).Select(x => x.Item1 != null && x.Item2 != null));
    }

    public ReactiveCommand<Unit, Unit> CreatePackage { get; set; }

    public IMutableFile File => file.Value;

    public ReactiveCommand<Unit, IMutableFile> SelectFile { get; set; }

    public IDirectory Directory => directory.Value;
    
    public ReactiveCommand<Unit, IDirectory> SelectDirectory { get; set; }
}
