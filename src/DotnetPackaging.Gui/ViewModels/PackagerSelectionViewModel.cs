using System.Collections.Generic;
using System.Linq;
using DotnetPackaging.Gui.Core;

namespace DotnetPackaging.Gui.ViewModels;

public class PackagerSelectionViewModel : ReactiveObject
{
    private PackageViewModel selectedPackager;

    public PackagerSelectionViewModel(IPackager[] packagers, Func<IPackager, PackageViewModel> createFunc)
    {
        PackagerViewModels = packagers.Select(createFunc).ToList();
        selectedPackager = PackagerViewModels.First();
    }

    public IReadOnlyCollection<PackageViewModel> PackagerViewModels { get; }

    public PackageViewModel SelectedPackager
    {
        get => selectedPackager;
        set => this.RaiseAndSetIfChanged(ref selectedPackager, value);
    }
}