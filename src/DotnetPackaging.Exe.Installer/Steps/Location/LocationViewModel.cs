using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace DotnetPackaging.Exe.Installer;

public partial class LocationViewModel : ReactiveValidationObject, IDisposable
{
    [Reactive]
    private string? installDirectory;

    public LocationViewModel()
    {
        this.ValidationRule(model => model.InstallDirectory, IsValidPath, "Path is not valid. Please choose a valid path.");
    }

    private static bool IsValidPath(string? s)
    {
        return Directory.Exists(s) && Path.IsPathRooted(s);
    }
}
