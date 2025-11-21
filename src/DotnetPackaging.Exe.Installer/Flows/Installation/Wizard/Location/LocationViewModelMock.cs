using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Location;

public class LocationViewModelMock : ILocationViewModel
{
    public string? InstallDirectory { get; set; }
    public IEnhancedCommand Browse { get; }
}
