using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

internal static class RuntimeFactory
{
    // Static type 2 runtime: it bundles squashfuse and libfuse, so the AppImage does not
    // depend on the host's glibc or on libfuse2 being installed.
    private static readonly Dictionary<Architecture, Uri> RuntimeUrls = new()
    {
        { Architecture.X86, new("https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-i686") },
        { Architecture.X64, new("https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64") },
        { Architecture.Arm32, new("https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-armhf") },
        { Architecture.Arm64, new("https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-aarch64") },
    };

    public static Task<Result<IRuntime>> Create(Architecture architecture)
    {
        return RuntimeUrls
            .TryFind(architecture).ToResult($"Could not find architecture {architecture}")
            .Bind(uri => uri.FromUri())
            .Map(IRuntime (source) => new Runtime(source, architecture));
    }
}
