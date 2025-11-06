using Avalonia.Controls;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace DotnetPackaging.InstallerStub;

public sealed partial class WizardWindow : Window
{
    public WizardWindow()
    {
        InitializeComponent();
        DataContext = new WizardViewModel();
    }
}

public sealed class WizardViewModel : ReactiveObject
{
    private string installDirectory = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "App");

    private string status = "Ready";

    public string InstallDirectory
    {
        get => installDirectory;
        set => this.RaiseAndSetIfChanged(ref installDirectory, value);
    }

    public string Status
    {
        get => status;
        set => this.RaiseAndSetIfChanged(ref status, value);
    }

    public ReactiveCommand<Unit, Unit> InstallCommand { get; }

    public WizardViewModel()
    {
        InstallCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            Status = "Installing...";
            await Task.Delay(200); // placeholder
            Status = "Done";
        });
    }
}
