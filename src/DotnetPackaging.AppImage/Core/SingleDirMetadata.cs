using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class SingleDirMetadata
{
    public required Maybe<string> AppName { get; init; }
    public required Maybe<string> StartupWmClass { get; set; }
    public required Maybe<IEnumerable<string>> Keywords { get; init; }
    public required Maybe<string> Comment { get; init; }
    public required Maybe<IEnumerable<string>> Categories { get; init; }
    public required Maybe<IIcon> Icon { get; init; }
}