using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class DesktopMetadata
{
    public required Maybe<string> Name { get; init; }
    public required Maybe<string> StartupWmClass { get; set; }
    public required Maybe<IEnumerable<string>> Keywords { get; init; }
    public required Maybe<string> Comment { get; init; }
    public required Maybe<IEnumerable<string>> Categories { get; init; }
    public required Maybe<string> ExecutablePath { get; init; }
}