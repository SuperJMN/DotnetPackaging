using System.Diagnostics;
using System.Text.Json;
using NuGet.Versioning;
using Serilog;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    private static readonly string[] PreferredFields = ["NuGetVersionV2", "NuGetVersion", "SemVer", "FullSemVer"];

    public static async Task<Result<string>> Run()
    {
        var toolResult = await RunGitVersion();
        if (toolResult.IsSuccess)
        {
            return toolResult;
        }

        Log.Warning("GitVersion failed: {Error}. Falling back to git describe", toolResult.Error);
        return await DescribeVersion();
    }

    private static async Task<Result<string>> RunGitVersion()
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

    private static async Task<Result<string>> DescribeVersion()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    ArgumentList = { "describe", "--tags", "--long", "--match", "*.*.*" },
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
                    ? $"git describe exited with code {process.ExitCode}"
                    : error;
                return Result.Failure<string>(string.IsNullOrWhiteSpace(message) ? "Unknown error" : message);
            }

            var description = output.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                return Result.Failure<string>("git describe produced no output");
            }

            var parts = description.TrimStart('v').Split('-');
            if (parts.Length < 3)
            {
                return Result.Failure<string>($"Unexpected git describe output '{description}'");
            }

            var tag = parts[0];
            if (!NuGet.Versioning.NuGetVersion.TryParse(tag, out var baseVersion))
            {
                return Result.Failure<string>($"Invalid tag '{tag}' in git describe output");
            }

            if (!int.TryParse(parts[1], out var commits))
            {
                return Result.Failure<string>($"Invalid commit count in git describe output '{description}'");
            }

            var version = commits == 0
                ? baseVersion.ToNormalizedString()
                : $"{baseVersion.ToNormalizedString()}-ci{commits}";

            return Result.Success(version);
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unknown error" : ex.Message;
            return Result.Failure<string>(message);
        }
    }

    private static bool GitVersionExists()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    ArgumentList = { "tool", "list", "--global" },
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Split('\n')
                .Any(line => line.TrimStart().StartsWith("gitversion.tool", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
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
                    ArgumentList = { "tool", "update", "--global", "GitVersion.Tool" },
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
                    ? $"dotnet tool update exited with code {process.ExitCode}"
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
                    FileName = "dotnet",
                    ArgumentList = { "gitversion", "-output", "json" },
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
                    ? $"gitversion exited with code {process.ExitCode}"
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
