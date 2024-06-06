using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Gui.ViewModels;

public static class OptionsMixin
{
    public static void CopyTo(this OptionsViewModel source, OptionsViewModel target)
    {
        target.Icon.File = source.Icon.File;
        target.Id.Value = source.Id.Value;
        target.StartupWMClass.Value = source.StartupWMClass.Value;
        target.Comment.Value = source.Comment.Value;
        target.Name.Value = source.Name.Value;
        target.Version.Value = source.Version.Value;
        target.Summary.Value = source.Summary.Value;
        target.AdditionalCategories.Clear();
        target.AdditionalCategories.AddRange(source.AdditionalCategories);
        target.MainCategory = source.MainCategory;
    }
    
    public static async Task<Result<Options>> ToOptions(this OptionsViewModel optionsViewModel)
    {
        var maybeIcon = await optionsViewModel.Icon.File.AsMaybe().Map(Icon.FromData);
        
        return maybeIcon.MapMaybe(i => new Options
        {
            Icon = i,
            Id = optionsViewModel.Id.Value.WhitespaceAsNone(),
            StartupWmClass= optionsViewModel.StartupWMClass.Value.WhitespaceAsNone(),
            Comment = optionsViewModel.Comment.Value.WhitespaceAsNone(),
            Name = optionsViewModel.Name.Value.WhitespaceAsNone(),
            Version = optionsViewModel.Version.Value.WhitespaceAsNone(),
            Summary = optionsViewModel.Summary.Value.WhitespaceAsNone(),
            AdditionalCategories = Maybe.From(optionsViewModel.AdditionalCategories.Select(Enum.Parse<AdditionalCategory>)),
            MainCategory = optionsViewModel.MainCategory.WhitespaceAsNone().Bind(s => Maybe.From(Enum.Parse<MainCategory>(s)))
        });
    }

    public static Maybe<string> WhitespaceAsNone(this string str)
    {
        return string.IsNullOrWhiteSpace(str) ? Maybe.None : str.AsMaybe();
    }
}