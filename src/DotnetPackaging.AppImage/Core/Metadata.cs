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
}