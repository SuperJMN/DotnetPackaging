using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetPackaging.Tool;

public static class ConsoleMixin
{
    public static void WriteResult(this Result result)
    {
        result
            .Tap(() => Log.Information("Success"))
            .TapError(Log.Error);
    }
    
    public static async Task WriteResult(this Task<Result> result)
    {
        await result
            .Tap(() => Log.Information("Success"))
            .TapError(Log.Error);
    }
}