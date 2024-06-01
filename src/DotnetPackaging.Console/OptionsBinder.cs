using System.CommandLine;
using System.CommandLine.Binding;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Console;

public class OptionsBinder(
    Option<string> appNameOption,
    Option<string> wmClassOption,
    Option<IEnumerable<string>> keywordsOption,
    Option<string> commentOption,
    Option<MainCategory?> mainCategoryOption,
    Option<IEnumerable<AdditionalCategory>> additionalCategoriesOption,
    Option<IIcon> iconOption, 
    Option<string> versionOption,
    Option<Uri> homePageOption,
    Option<string> licenseOption,
    Option<IEnumerable<Uri>> screenshotUrlsOption,
    Option<string> summaryOption,
    Option<string> appIdOption,
    Option<string> executableName)
    : BinderBase<Options>
{
    protected override Options GetBoundValue(BindingContext bindingContext)
    {
        return new Options
        {
            AppName = Maybe.From(bindingContext.ParseResult.GetValueForOption(appNameOption)!),
            StartupWmClass = Maybe.From(bindingContext.ParseResult.GetValueForOption(wmClassOption)!),
            Keywords = MaybeList(bindingContext, keywordsOption),
            Comment = Maybe.From(bindingContext.ParseResult.GetValueForOption(commentOption)!),
            MainCategory = MaybeEx.FromNullableStruct(bindingContext.ParseResult.GetValueForOption(mainCategoryOption)),
            AdditionalCategories = MaybeList(bindingContext, additionalCategoriesOption),
            Icon = Maybe<IIcon>.From(bindingContext.ParseResult.GetValueForOption(iconOption)!),
            Version = Maybe.From(bindingContext.ParseResult.GetValueForOption(versionOption)!),
            HomePage = Maybe.From(bindingContext.ParseResult.GetValueForOption(homePageOption)!),
            License = Maybe.From(bindingContext.ParseResult.GetValueForOption(licenseOption)!),
            ScreenshotUrls = MaybeList(bindingContext, screenshotUrlsOption),
            Summary = Maybe.From(bindingContext.ParseResult.GetValueForOption(summaryOption)!),
            AppId = Maybe.From(bindingContext.ParseResult.GetValueForOption(appIdOption)!),
            ExecutableName = Maybe.From(bindingContext.ParseResult.GetValueForOption(executableName)!),
        };
    }

    public Maybe<IEnumerable<T>> MaybeList<T>(BindingContext bindingContext, Option<IEnumerable<T>> option)
    {
        var value = bindingContext.ParseResult.GetValueForOption(option)!.ToList();
        return value.Any() ? value : Maybe.None;
    }
}