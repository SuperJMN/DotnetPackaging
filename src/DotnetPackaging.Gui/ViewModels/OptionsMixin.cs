using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Gui.ViewModels;

public static class OptionsMixin
{
    public static async Task<Result<Options>> ToOptions(this OptionsViewModel optionsViewModel)
    {
        var maybeIcon = await optionsViewModel.Icon.File.AsMaybe().Map(Icon.FromData);
        
        return maybeIcon.MapMaybe(i => new Options()
        {
            Icon = i,
            AppId = optionsViewModel.Id.Value,
            StartupWmClass= optionsViewModel.StartupWMClass.Value,
        });
    }
}