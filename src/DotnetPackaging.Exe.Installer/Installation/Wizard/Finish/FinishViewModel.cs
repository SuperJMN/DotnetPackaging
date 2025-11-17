using System.Diagnostics;
using System.Reactive;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;
using Zafiro.UI.Commands;
using CFE = CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Installer.Installation.Wizard.Finish;

public class FinishViewModel
{
    public FinishViewModel(InstallationResult result)
    {
        Result = result;
        LaunchOnClose = true;
        CreateDesktopShortcut = false;
        Close = ReactiveCommand.CreateFromTask<Unit, CFE.Result<InstallationResult>>(_ => Task.FromResult(ExecuteClose())).Enhance();
    }

    public bool LaunchOnClose { get; set; }
    public bool CreateDesktopShortcut { get; set; }

    public InstallationResult Result { get; }

    public IEnhancedCommand<CFE.Result<InstallationResult>> Close { get; }

    private CFE.Result<InstallationResult> ExecuteClose()
    {
        try
        {
            if (CreateDesktopShortcut)
            {
                ShortcutService.TryCreateDesktopShortcut(Result.Metadata.ApplicationName, Result.ExecutablePath);
            }

            if (LaunchOnClose)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Result.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(Result.ExecutablePath)!,
                    UseShellExecute = true
                });
            }

            return CFE.Result.Success(Result);
        }
        catch (Exception ex)
        {
            return CFE.Result.Failure<InstallationResult>($"Failed to finish: {ex.Message}");
        }
    }
}
