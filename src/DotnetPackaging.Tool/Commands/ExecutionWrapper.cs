using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;
using Serilog.Context;

namespace DotnetPackaging.Tool.Commands;

public static class ExecutionWrapper
{
    public static async Task ExecuteWithLogging(string commandName, string target, Func<ILogger, Task> action)
    {
        using var scope = LogContext.PushProperty("Command", commandName);
        var stopwatch = Stopwatch.StartNew();
        Log.Information("{Command} started for {Target}", commandName, target);
        var logger = Log.ForContext("Command", commandName).ForContext("Target", target);
        try
        {
            await action(logger);
            Log.Information("{Command} completed for {Target} in {Elapsed}", commandName, target, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Command} failed for {Target}", commandName, target);
            throw;
        }
    }
}
