using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Uninstallation.Wizard.Finish;
using DotnetPackaging.Exe.Installer.Uninstallation.Wizard.Steps;
using DotnetPackaging.Exe.Installer.Uninstallation.Wizard.Welcome;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace DotnetPackaging.Exe.Installer.Uninstallation.Wizard;

public class UninstallWizard
{
    private readonly IInstallerPayload payload;

    public UninstallWizard(IInstallerPayload payload)
    {
        this.payload = payload;
    }

    public SlimWizard<UninstallationResult> CreateWizard()
    {
        var welcome = new UninstallWelcomeViewModel(payload);

        return WizardBuilder
            .StartWith(() => welcome, "")
            .Next(w => w.Metadata.Value!).WhenValid()
            .Then(meta => new UninstallViewModel(meta), "Uninstall").NextCommand(vm => vm.Uninstall.Enhance("Uninstall"))
            .Then(result => new UninstallFinishedViewModel(result), "Finished").NextCommand(vm => vm.Close.Enhance("Close"))
            .WithCompletionFinalStep();
    }
}
