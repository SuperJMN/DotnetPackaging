using ReactiveUI;
using Zafiro.UI.Commands;

namespace DotnetPackaging.Exe.Installer.Flows.Installation.Wizard.Location;

public class LocationViewModelMock : ILocationViewModel
{
    public string? InstallDirectory { get; set; } = @"C:\Users\Jane\AppData\Local\Programs\Example App";
    public IEnhancedCommand Browse { get; } = ReactiveCommand.Create(() => { }).Enhance();
}
