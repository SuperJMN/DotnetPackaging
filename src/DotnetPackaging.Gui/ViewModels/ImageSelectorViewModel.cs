using System.Reactive;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.UI;

namespace DotnetPackaging.Gui.ViewModels;

public class ImageSelectorViewModel : ReactiveObject
{
    private readonly ObservableAsPropertyHelper<Maybe<IFile>> file;

    public ImageSelectorViewModel(IFileSystemPicker systemPicker)
    {
        PickFile = ReactiveCommand.CreateFromTask(() => systemPicker.PickForOpen(new FileTypeFilter("Images", ["bmp", "png", "jpg", "jpeg", "gif"])));
        file = PickFile.Successes().ToProperty(this, x => x.File);
    }

    public ReactiveCommand<Unit, Result<Maybe<IFile>>> PickFile { get; set; }

    public Maybe<IFile> File => file.Value;
}