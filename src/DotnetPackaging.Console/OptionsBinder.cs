﻿using System.CommandLine;
using System.CommandLine.Binding;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Console;

public class OptionsBinder(
    Option<string> appNameOption,
    Option<string> wmClassOption,
    Option<IEnumerable<string>> keywordsOption,
    Option<string> commentOption,
    Option<MainCategory?> mainCategory,
    Option<IEnumerable<AdditionalCategory>> categoriesOption,
    Option<IIcon> iconOption, 
    Option<string> versionOption,
    Option<Uri> homePageOption,
    Option<string> licenseOption,
    Option<IEnumerable<Uri>> screenshotUrlsOption,
    Option<string> summaryOption,
    Option<string> appIdOption)
    : BinderBase<Options>
{
    protected override Options GetBoundValue(BindingContext bindingContext)
    {
        return new Options
        {
            AppName = Maybe.From(bindingContext.ParseResult.GetValueForOption(appNameOption)!),
            StartupWmClass = Maybe.From(bindingContext.ParseResult.GetValueForOption(wmClassOption)!),
            Keywords = Maybe.From(bindingContext.ParseResult.GetValueForOption(keywordsOption)!),
            Comment = Maybe.From(bindingContext.ParseResult.GetValueForOption(commentOption)!),
            MainCategory = MaybeMixin.FromNullableStruct(bindingContext.ParseResult.GetValueForOption(mainCategory)),
            AdditionalCategories = Maybe.From(bindingContext.ParseResult.GetValueForOption(categoriesOption)!),
            Icon = Maybe<IIcon>.From(bindingContext.ParseResult.GetValueForOption(iconOption)!),
            Version = Maybe.From(bindingContext.ParseResult.GetValueForOption(versionOption)!),
            HomePage =  Maybe.From(bindingContext.ParseResult.GetValueForOption(homePageOption)!),
            License =  Maybe.From(bindingContext.ParseResult.GetValueForOption(licenseOption)!),
            ScreenshotUrls = Maybe.From(bindingContext.ParseResult.GetValueForOption(screenshotUrlsOption)!),
            Summary = Maybe.From(bindingContext.ParseResult.GetValueForOption(summaryOption)!),
            AppId = Maybe.From(bindingContext.ParseResult.GetValueForOption(appIdOption)!),
        };
    }
}

public class Options
{
    public Maybe<string> AppName { get; set; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; set; }
    public Maybe<string> Comment { get; set; }
    public Maybe<MainCategory> MainCategory { get; set; }
    public Maybe<IEnumerable<AdditionalCategory>> AdditionalCategories { get; set; }
    public Maybe<IIcon> Icon { get; set; }
    public Maybe<string> Version { get; set; }
    public Maybe<Uri> HomePage { get; set; }
    public Maybe<string> License { get; set; }
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; set; }
    public Maybe<string> Summary { get; set; }
    public Maybe<string> AppId { get; set; }
}