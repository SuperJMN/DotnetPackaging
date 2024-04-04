using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage;

public class AppImagePackager
{
    public static Task<Result> Build(Stream output, Stream runtime, Stream payload)
    {
        return Result.Try(async () =>
        {
            await runtime.CopyToAsync(output);
            await payload.CopyToAsync(output);
        });
    }
    
    public static Task<Result> Build(Stream output, Architecture architecture, IZafiroDirectory directory)
    {
        return RuntimeDownloader
            .GetRuntimeStream(architecture, new DefaultHttpClientFactory())
            .CombineAndBind(SquashFS.Build(directory), (runtime, payload) => Build(output, runtime, payload));
    }
    
    public static async Task<Result> Build(IZafiroFile output, Architecture architecture, IZafiroDirectory directory)
    {
        var ms = new MemoryStream();
        return await Build(ms, architecture, directory).Bind(() =>
        {
            ms.Position = 0;
            return output.SetData(ms);
        }).Map(() => ms);
    }
}