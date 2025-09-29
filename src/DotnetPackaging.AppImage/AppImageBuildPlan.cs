using Zafiro.DivineBytes;
using DotnetPackaging.AppImage.Metadata;

namespace DotnetPackaging.AppImage;

public record AppImageBuildPlan(
    string ExecutableName,
    string ExecutableTargetPath,
    string IconName,
    AppImageMetadata Metadata,
    RootContainer AppDir)
{
    public RootContainer ToRootContainer() => AppDir;
}
