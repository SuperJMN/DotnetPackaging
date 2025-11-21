using System.Reactive;
using DotnetPackaging.Exe.Installer.Core;
using ReactiveUI;
using Zafiro.UI.Commands;
using CFE = CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Installer.Flows.Uninstallation.Wizard.Finish;

public sealed class UninstallFinishedViewModel
{
    public UninstallFinishedViewModel(UninstallationResult result)
    {
        Result = result;
        Close = ReactiveCommand
            .CreateFromTask<Unit, CFE.Result<UninstallationResult>>(_ => Task.FromResult(ExecuteClose()))
            .Enhance();
    }

    public UninstallationResult Result { get; }

    public IEnhancedCommand<CFE.Result<UninstallationResult>> Close { get; }

    private CFE.Result<UninstallationResult> ExecuteClose()
        => CFE.Result.Success(Result);
}
