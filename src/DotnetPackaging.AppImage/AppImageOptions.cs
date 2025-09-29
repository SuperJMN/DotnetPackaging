using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage;

public class AppImageOptions
{
    // Optional icon name override. If None, defaults to metadata.IconName
    public Maybe<string> IconNameOverride { get; set; } = Maybe<string>.None;

}
