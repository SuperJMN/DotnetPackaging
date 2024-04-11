using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetPackaging.Console;

public static class ConsoleMixin
{
    public static async Task WriteResult<T>(this Task<Result<T>> result)
    {
        await result
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