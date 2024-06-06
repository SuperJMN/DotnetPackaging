using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI.Fody.Helpers;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Gui.ViewModels;

public class ImageSelectorViewModel : ReactiveObject
{
    public ImageSelectorViewModel(IFileSystemPicker systemPicker)
    {
        PickFile = ReactiveCommand.CreateFromTask(() => systemPicker.PickForOpen(new FileTypeFilter("Images", ["*.bmp", "*.png", "*.jpg", "*.jpeg", "*.gif"])));
        
        Reset = ReactiveCommand.Create(() =>
        {
            File = null;
        });
        
        PickFile.Successes()
            .Select(x => x.GetValueOrDefault())
            .BindTo(this, x => x.File);
    }

    public ReactiveCommand<Unit, Unit> Reset { get; }

    public ReactiveCommand<Unit, Result<Maybe<IFile>>> PickFile { get; }

    [Reactive]
    public IFile? File { get; set; }
}