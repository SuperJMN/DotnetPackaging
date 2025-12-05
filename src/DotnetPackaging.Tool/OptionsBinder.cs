using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

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
    private readonly Option<bool>? defaultLayout;

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
        Option<bool> isTerminal,
        Option<bool>? defaultLayout = null)
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
        this.defaultLayout = defaultLayout;
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
            IsTerminal = Maybe.From(parseResult.GetValue(isTerminal)),
            UseDefaultLayout = defaultLayout == null ? Maybe<bool>.None : Maybe.From(parseResult.GetValue(defaultLayout))
        };
    }

    private Maybe<IEnumerable<T>> MaybeList<T>(ParseResult parseResult, Option<IEnumerable<T>> option)
    {
        var value = parseResult.GetValue(option)?.ToList() ?? [];
        return value.Any() ? value : Maybe.None;
    }

    public static IIcon? GetIcon(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var iconPath = result.Tokens[0].Value;
        var fs = new System.IO.Abstractions.FileSystem();
        var fileInfo = fs.FileInfo.New(iconPath);
        if (!fileInfo.Exists)
        {
            result.AddError($"Invalid icon '{iconPath}': File not found");
            return null;
        }

        try
        {
            var iconResult = DotnetPackaging.Icon.FromByteSource(ByteSource.FromStreamFactory(() => fileInfo.OpenRead())).GetAwaiter().GetResult();
            if (iconResult.IsFailure)
            {
                result.AddError($"Invalid icon '{iconPath}': {iconResult.Error}");
                return null;
            }

            return iconResult.Value;
        }
        catch (Exception ex)
        {
            result.AddError($"Invalid icon '{iconPath}': {ex.Message}");
            return null;
        }
    }
}
