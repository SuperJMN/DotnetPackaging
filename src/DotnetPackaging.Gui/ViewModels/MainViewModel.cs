using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Storage;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Mutable;
using Zafiro.Reactive;
using Zafiro.UI;

namespace DotnetPackaging.Gui.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableAsPropertyHelper<IDirectory?> directory;
    private readonly ObservableAsPropertyHelper<IMutableFile?> file;
    private readonly CompositeDisposable disposable = new();

    public MainViewModel(IFileSystemPicker systemPicker, INotificationService notificationService)
    {
        OptionsViewModel = new OptionsViewModel(systemPicker);
        SelectDirectory = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(systemPicker.PickFolder).Values().SelectMany(x => x.ToImmutable()).Successes());
        SelectDirectory
            .Do(d => OptionsViewModel.Id.Value = d.Name)
            .Subscribe()
            .DisposeWith(disposable);

        SelectFile = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(() => systemPicker.PickForSave(Directory?.Name ?? "Package", "appImage", new FileTypeFilter("AppImage", ["appimage"]))).Values());

        directory = SelectDirectory.ToProperty(this, x => x.Directory);
        file = SelectFile.ToProperty(this, x => x.File);
        var canCreatePackage = this.WhenAnyValue(x => x.Directory, x => x.File).NotNull().And(OptionsViewModel.IsValid());
        CreatePackage = ReactiveCommand.CreateFromTask(() => OptionsViewModel.ToOptions().Bind(options => CreateAppImage(Directory!, File!, options)), canCreatePackage);
        CreatePackage.HandleErrorsWith(notificationService);
        IsBusy = CreatePackage.IsExecuting.Merge(SelectDirectory.IsExecuting);
    }

    private static Task<Result> CreateAppImage(IDirectory directory, IMutableFile mutableFile, Options options)
    {
        if (directory == null)
        {
            throw new ArgumentNullException(nameof(directory));
        }

        if (mutableFile == null)
        {
            throw new ArgumentNullException(nameof(mutableFile));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return AppImage.AppImage
            .From()
            .Directory(directory)
            .Configure(x => x.From(options)).Build()
            .Bind(image => image.ToData()
                .Bind(mutableFile.SetContents));
    }

    public IObservable<bool> IsBusy { get; }


    public OptionsViewModel OptionsViewModel { get; set; }

    public ReactiveCommand<Unit, Result> CreatePackage { get; set; }

    public IMutableFile? File => file.Value;

    public ReactiveCommand<Unit, IMutableFile> SelectFile { get; set; }

    public IDirectory? Directory => directory.Value;

    public ReactiveCommand<Unit, IDirectory> SelectDirectory { get; set; }

    public void Dispose()
    {
        directory.Dispose();
        file.Dispose();
        disposable.Dispose();
        OptionsViewModel.Dispose();
        CreatePackage.Dispose();
        SelectFile.Dispose();
        SelectDirectory.Dispose();
    }
}