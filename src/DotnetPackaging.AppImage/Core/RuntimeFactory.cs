using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

internal static class RuntimeFactory
{
    private static readonly Dictionary<Architecture, Uri> RuntimeUrls = new()
    {
        { Architecture.X86, new ("https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-x86_64") },
        { Architecture.X64, new ("https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-x86_64") },
        { Architecture.Arm32, new("https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-armhf") },
        { Architecture.Arm64, new("https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-aarch64") },
    };

    public static Task<Result<IRuntime>> Create(Architecture architecture)
    {
        return RuntimeUrls
            .TryFind(architecture).ToResult($"Could not find architecture {architecture}")
            .Bind(uri => uri.FromUri())
            .Map(IRuntime (source) => new Runtime(source, architecture));
    }
}
