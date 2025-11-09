using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;

namespace DotnetPackaging.Exe.Installer.Steps.Location;

public partial class LocationViewModel : ReactiveValidationObject, IValidatable, ILocationViewModel
{
    [Reactive]
    private string? installDirectory;

    public LocationViewModel()
    {
        this.ValidationRule<LocationViewModel, string>(model => model.InstallDirectory, IsValidPath, "Path is not valid. Please choose a valid path.");
    }

    private static bool IsValidPath(string? s)
    {
        return Directory.Exists(s) && Path.IsPathRooted(s);
    }

    public IObservable<bool> IsValid => this.IsValid();
}
