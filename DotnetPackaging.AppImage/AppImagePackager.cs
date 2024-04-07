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
}