using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Tool;

public class OptionsBinder
{
    private readonly Option<string> appNameOption;
    private readonly Option<string> wmClassOption;
    private readonly Option<IEnumerable<string>> keywordsOption;
    private readonly Option<string> commentOption;
    private readonly Option<MainCategory?> mainCategoryOption;
    private readonly Option<IEnumerable<AdditionalCategory>> additionalCategoriesOption;
    private readonly Option<IIcon?> iconOption;
    private readonly Option<string> versionOption;
    private readonly Option<Uri> homePageOption;
    private readonly Option<string> licenseOption;
    private readonly Option<IEnumerable<Uri>> screenshotUrlsOption;
    private readonly Option<string> summaryOption;
    private readonly Option<string> appIdOption;
    private readonly Option<string> executableName;
    private readonly Option<bool> isTerminal;

    public OptionsBinder(
        Option<string> appNameOption,
        Option<string> wmClassOption,
        Option<IEnumerable<string>> keywordsOption,
        Option<string> commentOption,
        Option<MainCategory?> mainCategoryOption,
        Option<IEnumerable<AdditionalCategory>> additionalCategoriesOption,
        Option<IIcon?> iconOption,
        Option<string> versionOption,
        Option<Uri> homePageOption,
        Option<string> licenseOption,
        Option<IEnumerable<Uri>> screenshotUrlsOption,
        Option<string> summaryOption,
        Option<string> appIdOption,
        Option<string> executableName,
        Option<bool> isTerminal)
    {
        this.appNameOption = appNameOption;
        this.wmClassOption = wmClassOption;
        this.keywordsOption = keywordsOption;
        this.commentOption = commentOption;
        this.mainCategoryOption = mainCategoryOption;
        this.additionalCategoriesOption = additionalCategoriesOption;
        this.iconOption = iconOption;
        this.versionOption = versionOption;
        this.homePageOption = homePageOption;
        this.licenseOption = licenseOption;
        this.screenshotUrlsOption = screenshotUrlsOption;
        this.summaryOption = summaryOption;
        this.appIdOption = appIdOption;
        this.executableName = executableName;
        this.isTerminal = isTerminal;
    }

    public Options Bind(ParseResult parseResult)
    {
        var icon = parseResult.GetValue(iconOption);
        return new Options
        {
            Name = Maybe.From(parseResult.GetValue(appNameOption)!),
            StartupWmClass = Maybe.From(parseResult.GetValue(wmClassOption)!),
            Keywords = MaybeList(parseResult, keywordsOption),
            Comment = Maybe.From(parseResult.GetValue(commentOption)!),
            MainCategory = MaybeEx.FromNullableStruct(parseResult.GetValue(mainCategoryOption)),
            AdditionalCategories = MaybeList(parseResult, additionalCategoriesOption),
            Icon = Maybe<IIcon>.From(icon!),
            Version = Maybe.From(parseResult.GetValue(versionOption)!),
            HomePage = Maybe.From(parseResult.GetValue(homePageOption)!),
            License = Maybe.From(parseResult.GetValue(licenseOption)!),
            ScreenshotUrls = MaybeList(parseResult, screenshotUrlsOption),
            Summary = Maybe.From(parseResult.GetValue(summaryOption)!),
            Id = Maybe.From(parseResult.GetValue(appIdOption)!),
            ExecutableName = Maybe.From(parseResult.GetValue(executableName)!),
            IsTerminal = Maybe.From(parseResult.GetValue(isTerminal))
        };
    }

    private Maybe<IEnumerable<T>> MaybeList<T>(ParseResult parseResult, Option<IEnumerable<T>> option)
    {
        var value = parseResult.GetValue(option)?.ToList() ?? [];
        return value.Any() ? value : Maybe.None;
    }
}