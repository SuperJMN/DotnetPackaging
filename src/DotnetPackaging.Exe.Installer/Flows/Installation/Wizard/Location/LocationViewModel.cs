using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Location;

public partial class LocationViewModel : ReactiveValidationObject, IValidatable, ILocationViewModel
{
    [Reactive]
    private string? installDirectory;

    public LocationViewModel(IFolderPickerService folderPicker, string? defaultInstallDirectory = null)
    {
        // Allow rooted paths even if they don't exist yet; the installer will create them
        this.ValidationRule<LocationViewModel, string>(model => model.InstallDirectory, IsValidPath, "Path is not valid. Please choose a valid path.");

        if (!string.IsNullOrWhiteSpace(defaultInstallDirectory))
        {
            InstallDirectory = defaultInstallDirectory;
        }

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
        return !string.IsNullOrWhiteSpace(s) && Path.IsPathRooted(s);
    }

    public IObservable<bool> IsValid => this.IsValid();
}
