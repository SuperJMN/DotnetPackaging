using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Steps.Location;

public class LocationViewModelMock : ILocationViewModel
{
    public string? InstallDirectory { get; set; }
    public IEnhancedCommand Browse { get; }
}
