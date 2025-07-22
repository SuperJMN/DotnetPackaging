using System.Diagnostics;
using Serilog;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    public static async Task<Result<string>> Run()
    {
        if (!GitVersionExists())
        {
            var installResult = await Install();
            if (installResult.IsFailure)
            {
                return Result.Failure<string>(installResult.Error);
            }
        }

        return await Execute();
    }

    private static bool GitVersionExists()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return pathEnv.Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, $"dotnet-gitversion{extension}"))
            .Any(File.Exists);
    }

    private static async Task<Result> Install()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    ArgumentList = { "tool", "install", "--global", "GitVersion.Tool" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0
                ? Result.Success()
                : Result.Failure(error);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static async Task<Result<string>> Execute()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet-gitversion",
                    ArgumentList = { "/showvariable", "NuGetVersionV2" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log.Warning("GitVersion failed: {Error}", error);
                return Result.Failure<string>(error);
            }

            var version = output.Trim();
            return string.IsNullOrWhiteSpace(version)
                ? Result.Failure<string>("GitVersion produced no output")
                : Result.Success(version);
        }
        catch (Exception ex)
        {
            Log.Warning("GitVersion invocation failed: {Message}", ex.Message);
            return Result.Failure<string>(ex.Message);
        }
    }
}
