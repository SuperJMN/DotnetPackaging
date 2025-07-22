using System.Diagnostics;
using System.Text.Json;
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
                : Result.Failure(string.IsNullOrWhiteSpace(error)
                    ? $"GitVersion installation failed with exit code {process.ExitCode}"
                    : error);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static readonly string[] PreferredFields = ["NuGetVersionV2", "NuGetVersion", "SemVer", "FullSemVer"];

    private static async Task<Result<string>> Execute()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet-gitversion",
                    ArgumentList = { "-output", "json" },
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
                var message = string.IsNullOrWhiteSpace(error)
                    ? $"GitVersion exited with code {process.ExitCode}"
                    : error;
                Log.Warning("GitVersion failed: {Error}", message);
                return Result.Failure<string>(message);
            }

            try
            {
                using var document = JsonDocument.Parse(output);
                var root = document.RootElement;
                foreach (var field in PreferredFields)
                {
                    if (root.TryGetProperty(field, out var property))
                    {
                        var value = property.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return Result.Success(value);
                        }
                    }
                }

                return Result.Failure<string>("No version fields found in GitVersion output");
            }
            catch (Exception parseEx)
            {
                Log.Warning("GitVersion JSON parsing failed: {Message}", parseEx.Message);
                return Result.Failure<string>(parseEx.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("GitVersion invocation failed: {Message}", ex.Message);
            return Result.Failure<string>(ex.Message);
        }
    }
}
