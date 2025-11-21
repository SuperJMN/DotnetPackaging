using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Location;

public interface ILocationViewModel
{
    string? InstallDirectory { get; set; }
    IEnhancedCommand Browse { get; }
}
