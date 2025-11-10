using System.Diagnostics;
using System.Reactive;
using CFE = CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.UI.Commands;
using System.Threading.Tasks;
using DotnetPackaging.Exe.Installer.Core;

namespace DotnetPackaging.Exe.Installer.Steps.Finish;

public class FinishViewModel
{
    public FinishViewModel(InstallationResult result)
    {
        Result = result;
        LaunchOnClose = true;
        Close = ReactiveCommand.CreateFromTask<Unit, CFE.Result<InstallationResult>>(_ => Task.FromResult(ExecuteClose())).Enhance();
    }

    public bool LaunchOnClose { get; set; }

    public InstallationResult Result { get; }

    public IEnhancedCommand<CFE.Result<InstallationResult>> Close { get; }

    private CFE.Result<InstallationResult> ExecuteClose()
    {
        try
        {
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
            return CFE.Result.Failure<InstallationResult>($"Failed to launch application: {ex.Message}");
        }
    }
}
