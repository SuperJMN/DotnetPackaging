using System.Diagnostics;
using System.Text.Json;
using Serilog;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    private static readonly string[] PreferredFields = ["NuGetVersionV2", "NuGetVersion", "SemVer", "FullSemVer"];

    public static async Task<Result<string>> Run()
    {
        if (!GitVersionExists())
        {
            var installResult = await InstallTool();
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

    private static async Task<Result> InstallTool()
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

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(error)
                    ? $"dotnet tool install exited with code {process.ExitCode}"
                    : error;
                return Result.Failure(string.IsNullOrWhiteSpace(message) ? "Unknown error" : message);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unknown error" : ex.Message;
            return Result.Failure(message);
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
                    ? $"dotnet-gitversion exited with code {process.ExitCode}"
                    : error;
                Log.Warning("GitVersion failed: {Error}", message);
                return Result.Failure<string>(string.IsNullOrWhiteSpace(message) ? "Unknown error" : message);
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
                var message = string.IsNullOrWhiteSpace(parseEx.Message) ? "Unknown error" : parseEx.Message;
                return Result.Failure<string>(message);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("GitVersion invocation failed: {Message}", ex.Message);
            var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unknown error" : ex.Message;
            return Result.Failure<string>(message);
        }
    }
}
