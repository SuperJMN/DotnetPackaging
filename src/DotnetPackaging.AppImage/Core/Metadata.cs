using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public record Metadata
{
    public required string AppName { get; init; }
    public required Maybe<string> StartupWmClass { get; set; }
    public required Maybe<IEnumerable<string>> Keywords { get; init; }
    public required Maybe<string> Comment { get; init; }
    public required Maybe<Categories> Categories { get; init; }
    public required Maybe<IIcon> Icon { get; init; }
    public required Maybe<string> Version { get; init; }
    public required Maybe<Uri> HomePage { get; init; }
    public required Maybe<IEnumerable<Uri>> ScreenshotUrls { get; init; }
    public required Maybe<string> Summary { get; init; }
    public required Maybe<string> License { get; init; }
    public required Maybe<string> AppId { get; init; }
}