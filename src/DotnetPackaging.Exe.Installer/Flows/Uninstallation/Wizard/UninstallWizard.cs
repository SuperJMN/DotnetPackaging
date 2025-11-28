using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Flows.Uninstallation.Wizard.Finish;
using DotnetPackaging.Exe.Installer.Flows.Uninstallation.Wizard.Steps;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace DotnetPackaging.Exe.Installer.Flows.Uninstallation.Wizard;

public class UninstallWizard
{
    private readonly IInstallerPayload payload;

    public UninstallWizard(IInstallerPayload payload)
    {
        this.payload = payload;
    }

    public SlimWizard<UninstallationResult> CreateWizard()
    {
        var welcome = new WelcomeViewModel(payload, name => $"{name} Uninstaller", "uninstall", "from");

        return WizardBuilder
            .StartWith(() => welcome, "").Next(w => w.Metadata.Value!).WhenValid()
            .Then(meta => new UninstallViewModel(meta), "Uninstall").NextCommand(vm => vm.Uninstall.Enhance("Uninstall"))
            .Then(result => new UninstallFinishedViewModel(result), "Finished").NextCommand(vm => vm.Close.Enhance("Close"))
            .WithCompletionFinalStep();
    }
}
