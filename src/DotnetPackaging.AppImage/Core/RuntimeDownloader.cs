using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class RuntimeDownloader
{
    private static readonly Dictionary<Architecture, string> RuntimeUrls = new()
    {
        { Architecture.X86, "https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-x86_64" },
        { Architecture.X64, "https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-x86_64" },
        { Architecture.Arm, "https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-armhf" },
        { Architecture.Arm64, "https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-aarch64" },
    };

    public static Task<Result<Stream>> GetRuntimeStream(Architecture architecture, IHttpClientFactory httpClientFactory)
    {
        if (!RuntimeUrls.TryGetValue(architecture, out var runtimeUrl))
        {
            throw new ArgumentException("Invalid architecture", nameof(architecture));
        }
        return FetchStream(runtimeUrl);
    }
    private static Task<Result<Stream>> FetchStream(string runtimeUrl)
    {
        return Http.Instance.GetStream(runtimeUrl).Map(stream => (Stream)stream);
    }
}