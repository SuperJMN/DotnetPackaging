using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

public record FlatpakBuildPlan(
    string CommandName,
    string ExecutableTargetPath,
    string AppId,
    PackageMetadata Metadata,
    RootContainer Layout)
{
    public RootContainer ToRootContainer() => Layout;
}
