using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetPackaging.Tool;

public static class ConsoleMixin
{
    public static void WriteResult(this Result result)
    {
        if (result.IsFailure)
        {
            Log.Error("Operation failed: {Error}", result.Error);
            Environment.ExitCode = 1;
        }
        else
        {
            Log.Information("Operation completed successfully");
        }
    }
    
    public static async Task WriteResult(this Task<Result> resultTask)
    {
        var result = await resultTask;
        if (result.IsFailure)
        {
            Log.Error("Operation failed: {Error}", result.Error);
            Environment.ExitCode = 1;
        }
        else
        {
            Log.Information("Operation completed successfully");
        }
    }
}