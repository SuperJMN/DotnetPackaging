using DotnetPackaging.Gui.Core;

namespace DotnetPackaging.Gui.ViewModels;

public class PackagerSelectionViewModel
{
    public IPackager[] Packagers { get; }

    public PackagerSelectionViewModel(IPackager[] packagers)
    {
        Packagers = packagers;
    }

    public IPackager SelectedPackager { get; set; }
}