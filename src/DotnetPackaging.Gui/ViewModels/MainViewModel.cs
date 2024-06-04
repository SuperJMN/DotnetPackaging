using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Storage;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Mutable;
using Zafiro.Reactive;
using Zafiro.UI;

namespace DotnetPackaging.Gui.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableAsPropertyHelper<FileSystemNodeViewModel<IMutableDirectory>?> directory;
    private readonly ObservableAsPropertyHelper<FileSystemNodeViewModel<IMutableFile>?> file;
    private readonly CompositeDisposable disposable = new();

    public MainViewModel(IFileSystemPicker systemPicker, INotificationService notificationService, IDialogService dialogService)
    {
        OptionsViewModel = new OptionsViewModel(systemPicker);
        SelectDirectory = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(systemPicker.PickFolder).Values().Select(x => new FileSystemNodeViewModel<IMutableDirectory>(x)));
        SelectDirectory
            .Do(d =>
            {
                OptionsViewModel.Id.Value = d.Name;
            })
            .Subscribe()
            .DisposeWith(disposable);

        SelectFile = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(() => systemPicker.PickForSave(Directory?.Name ?? "Package", "appImage", new FileTypeFilter("AppImage", ["*.appimage"]))).Values().Select(x => new FileSystemNodeViewModel<IMutableFile>(x)));

        directory = SelectDirectory.ToProperty(this, x => x.Directory);
        file = SelectFile.ToProperty(this, x => x.File);
        var canCreatePackage = this.WhenAnyValue(x => x.Directory, x => x.File).NotNull().And(OptionsViewModel.IsValid());
        CreatePackage = ReactiveCommand.CreateFromTask(() => Directory!.Value.ToImmutable().Bind(dir => OptionsViewModel.ToOptions().Bind(options => CreateAppImage(dir!, File!.Value, options))), canCreatePackage);
        CreatePackage.HandleErrorsWith(notificationService);
        IsBusy = CreatePackage.IsExecuting.Merge(SelectDirectory.IsExecuting);

        CreatePackage.Successes()
            .SelectMany(_ => Observable.FromAsync(() => dialogService.ShowMessage("Package creation", "The creation of the AppImage has been successful!")))
            .Subscribe()
            .DisposeWith(disposable);
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

    public FileSystemNodeViewModel<IMutableFile>? File => file.Value;

    public ReactiveCommand<Unit, FileSystemNodeViewModel<IMutableFile>> SelectFile { get; set; }

    public FileSystemNodeViewModel<IMutableDirectory>? Directory => directory.Value;

    public ReactiveCommand<Unit, FileSystemNodeViewModel<IMutableDirectory>> SelectDirectory { get; set; }

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