using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage;

public class AppImageOptions
{
    // Optional icon name override. If None, defaults to metadata.IconName
    public Maybe<string> IconNameOverride { get; set; } = Maybe<string>.None;

    // If true and a PNG icon is found, also write a .DirIcon at AppDir root
    public bool EnableDirIcon { get; set; } = false;
}