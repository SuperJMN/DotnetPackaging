using System.CommandLine;
using System.CommandLine.Binding;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.Console;

public class SingleDirOptionsBinder(
    Option<string> option,
    Option<string> wmClassOption,
    Option<IEnumerable<string>> keywordsOption,
    Option<string> commentOption,
    Option<MainCategory?> mainCategory,
    Option<IEnumerable<AdditionalCategory>> categoriesOption,
    Option<IIcon> iconOption, 
    Option<string> versionOption)
    : BinderBase<Options>
{
    protected override Options GetBoundValue(BindingContext bindingContext)
    {
        var valueForOption = bindingContext.ParseResult.GetValueForOption(mainCategory);
        return new Options
        {
            AppName = Maybe.From(bindingContext.ParseResult.GetValueForOption(option)!),
            StartupWmClass = Maybe.From(bindingContext.ParseResult.GetValueForOption(wmClassOption)!),
            Keywords = Maybe.From(bindingContext.ParseResult.GetValueForOption(keywordsOption)!),
            Comment = Maybe.From(bindingContext.ParseResult.GetValueForOption(commentOption)!),
            MainCategory = MaybeMixin.From(valueForOption),
            AdditionalCategories = Maybe.From(bindingContext.ParseResult.GetValueForOption(categoriesOption)!),
            Icon = Maybe<IIcon>.From(bindingContext.ParseResult.GetValueForOption(iconOption)!),
            Version = Maybe.From(bindingContext.ParseResult.GetValueForOption(versionOption)!),
        };
    }
}