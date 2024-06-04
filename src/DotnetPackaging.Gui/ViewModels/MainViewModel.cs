using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DotnetPackaging.AppImage.Core;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Simple;
using Zafiro.Avalonia.Storage;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem.Mutable;
using Zafiro.Reactive;

namespace DotnetPackaging.Gui.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableAsPropertyHelper<FileSystemNodeViewModel<IMutableDirectory>?> directory;
    private readonly CompositeDisposable disposable = new();
    private readonly ObservableAsPropertyHelper<FileSystemNodeViewModel<IMutableFile>?> file;

    public MainViewModel(IFileSystemPicker systemPicker, INotificationService notificationService, ISimpleDialog dialog)
    {
        OptionsViewModel = new OptionsViewModel(systemPicker);

        var canBrowse = new Subject<bool>();
        
        SelectDirectory = ReactiveCommand.CreateFromObservable(() => SelectInputDirectory(systemPicker), canBrowse);
        SelectDirectory
            .Do(d => { OptionsViewModel.Id.Value = d.Name; })
            .Subscribe()
            .DisposeWith(disposable);
        directory = SelectDirectory.ToProperty(this, x => x.Directory);
        
        SelectFile = ReactiveCommand.CreateFromObservable(() => SelectOutputFile(systemPicker), canBrowse);
        file = SelectFile.ToProperty(this, x => x.File);
        
        var canCreatePackage = this.WhenAnyValue(x => x.Directory, x => x.File).NotNull().And(OptionsViewModel.IsValid());
        CreatePackage = ReactiveCommand.CreateFromTask(Pack, canCreatePackage);
        CreatePackage.HandleErrorsWith(notificationService);

        CreatePackage.IsExecuting.Not().Subscribe(canBrowse).DisposeWith(disposable);
        
        IsBusy = CreatePackage.IsExecuting;
        
        CreatePackage.Successes()
            .SelectMany(_ => Observable.FromAsync(() => dialog.ShowMessage("Package creation", "The creation of the AppImage has been successful!")))
            .Subscribe()
            .DisposeWith(disposable);
        ShowMetadata = ReactiveCommand.CreateFromTask(async () =>
        {
            var optionsViewModel = new OptionsViewModel(systemPicker, OptionsViewModel);
            await dialog.Show(optionsViewModel, "Options", optionsViewModel.IsValid());
            OptionsViewModel = optionsViewModel;
        });
    }

    public ReactiveCommand<Unit, Unit> ShowMetadata { get; set; }

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

    private static IObservable<FileSystemNodeViewModel<IMutableDirectory>> SelectInputDirectory(IFileSystemPicker systemPicker)
    {
        return Observable.FromAsync(systemPicker.PickFolder).Values().Select(x => new FileSystemNodeViewModel<IMutableDirectory>(x));
    }

    private static Task<Result> CreateAppImage(IDirectory directory, IMutableFile mutableFile, Options options)
    {
        return AppImage.AppImage
            .From()
            .Directory(directory)
            .Configure(x => x.From(options)).Build()
            .Bind(image => image.ToData()
                .Bind(mutableFile.SetContents));
    }

    private IObservable<FileSystemNodeViewModel<IMutableFile>> SelectOutputFile(IFileSystemPicker systemPicker)
    {
        return Observable
            .FromAsync(() => systemPicker
                .PickForSave(Directory?.Name ?? "Package", "appImage", new FileTypeFilter("AppImage", ["*.appimage"])))
            .Values()
            .Select(x => new FileSystemNodeViewModel<IMutableFile>(x));
    }

    private Task<Result> Pack()
    {
        return Directory!.Value.ToImmutable()
            .Bind(dir => OptionsViewModel
                .ToOptions()
                .Bind(options => CreateAppImage(dir!, File!.Value, options)));
    }
}