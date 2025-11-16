using System.Windows.Input;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Steps.Location;

public interface ILocationViewModel
{
    string? InstallDirectory { get; set; }
    IEnhancedCommand Browse { get; }
}
