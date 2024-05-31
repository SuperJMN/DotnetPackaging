using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.UI;

namespace DotnetPackaging.Gui.ViewModels;

public class ImageSelectorViewModel : ReactiveObject
{
    private readonly ObservableAsPropertyHelper<IFile?> file;
    private readonly Subject<Maybe<IFile>> fileSubject = new();

    public ImageSelectorViewModel(IFileSystemPicker systemPicker)
    {
        PickFile = ReactiveCommand.CreateFromTask(() => systemPicker.PickForOpen(new FileTypeFilter("Images", ["*.bmp", "*.png", "*.jpg", "*.jpeg", "*.gif"])));
        Reset = ReactiveCommand.Create(() => fileSubject.OnNext(Maybe.None));
        file = PickFile.Successes().Merge(fileSubject).Select(x => x.GetValueOrDefault()).ToProperty(this, x => x.File);
    }

    public ReactiveCommand<Unit, Unit> Reset { get; }

    public ReactiveCommand<Unit, Result<Maybe<IFile>>> PickFile { get; }

    public IFile? File => file.Value;
}