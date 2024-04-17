using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.AppImage;

public class Options
{
    public required Maybe<string> AppName { get; set; }
    public required Maybe<string> Version { get; set; }
    public required Maybe<string> StartupWmClass { get; set; }
    public required Maybe<IEnumerable<string>> Keywords { get; set; }
    public required Maybe<string> Comment { get; set; }
    public required Maybe<MainCategory> MainCategory { get; set; }
    public required Maybe<IEnumerable<AdditionalCategory>> AdditionalCategories { get; set; }
    public required Maybe<IIcon> Icon { get; set; }
    public required Maybe<Uri> HomePage { get; init; }
    public required Maybe<string> Summary { get; init; }
    public required Maybe<string> License { get; init; }
    public required Maybe<IEnumerable<Uri>> ScreenshotUrls { get; init; }
    public required Maybe<string> AppId { get; init; }
}