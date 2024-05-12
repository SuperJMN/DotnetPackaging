using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public record PackageMetadata
{
    public string AppName { get; init; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; init; }
    public Maybe<string> Comment { get; init; }
    public Maybe<Categories> Categories { get; init; } = Maybe<Categories>.None;
    public Maybe<IIcon> Icon { get; init; }
    public Maybe<string> Version { get; init; }
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; init; }
    public Maybe<string> Summary { get; init; }
    public Maybe<string> License { get; init; }
    public Maybe<string> AppId { get; init; }
    public string Package { get; set; }
    public Maybe<string> Section { get; set; }
    public Maybe<string> Priority { get; set; }
    public required Architecture Architecture { get; init; }
    public Maybe<string> Maintainer { get; set; }
    public Maybe<string> Description { get; set; }
    public Maybe<Uri> Homepage { get; set; }
    public Maybe<string> Recommends { get; set; }
    public Maybe<string> VcsGit { get; set; }
    public Maybe<string> VcsBrowser { get; set; }
    public Maybe<long> InstalledSize { get; set; }
    public Maybe<DateTimeOffset> ModificationTime { get; set; }
}