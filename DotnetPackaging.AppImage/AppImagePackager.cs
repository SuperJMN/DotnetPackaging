using System.Runtime.InteropServices;
using ClassLibrary1;
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
    
    public static Task<Result> Build(Stream output, Architecture architecture, IDataTree dataTree)
    {
        return RuntimeDownloader
            .GetRuntimeStream(architecture, new DefaultHttpClientFactory())
            .CombineAndBind(SquashFS.Build(dataTree), (runtime, payload) => Build(output, runtime, payload));
    }
}