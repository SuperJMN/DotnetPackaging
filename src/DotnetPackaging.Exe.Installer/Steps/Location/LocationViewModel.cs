using System.Reactive;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;
using DotnetPackaging.Exe.Installer;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Steps.Location;

public partial class LocationViewModel : ReactiveValidationObject, IValidatable, ILocationViewModel
{
    [Reactive]
    private string? installDirectory;

    public LocationViewModel(IFolderPickerService folderPicker)
    {
        this.ValidationRule<LocationViewModel, string>(model => model.InstallDirectory, IsValidPath, "Path is not valid. Please choose a valid path.");

        Browse = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await folderPicker.PickFolder();
            if (!string.IsNullOrWhiteSpace(path))
            {
                InstallDirectory = path;
            }
        }).Enhance();
    }

    public IEnhancedCommand Browse { get; }

    private static bool IsValidPath(string? s)
    {
        return Directory.Exists(s) && Path.IsPathRooted(s);
    }

    public IObservable<bool> IsValid => this.IsValid();
}
