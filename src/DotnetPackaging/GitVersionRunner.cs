using System.Diagnostics;
using System.Text.Json;
using NuGet.Versioning;
using Serilog;
using DotnetPackaging.Deployment.Core;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    public static async Task<Result<string>> Run(string? startPath = null)
    {
        var repositoryResult = FindGitRoot(startPath ?? Environment.CurrentDirectory);
        var repositoryPath = repositoryResult.GetValueOrDefault(startPath ?? Environment.CurrentDirectory);

        var toolResult = GitVersionExists() ? Result.Success() : await Install();
        if (toolResult.IsFailure)
        {
            Log.Warning("GitVersion installation failed: {Error}. Falling back to git describe", toolResult.Error);
            return await DescribeVersion(repositoryPath);
        }

        var versionResult = await Execute(repositoryPath);
        if (versionResult.IsSuccess)
        {
            return versionResult;
        }

        Log.Warning("GitVersion failed: {Error}. Falling back to git describe", versionResult.Error);
        return await DescribeVersion(repositoryPath);
    }

    private static bool GitVersionExists()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return pathEnv.Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, $"dotnet-gitversion{extension}"))
            .Any(File.Exists);
    }

    private static Task<Result> Install()
    {
        var logger = Maybe<ILogger>.From(Log.Logger);
        var command = new Command(logger);
        return command.Execute("dotnet", "tool install --global GitVersion.Tool");
    }

    private static readonly string[] PreferredFields = ["NuGetVersionV2", "NuGetVersion", "SemVer", "FullSemVer"];

    private static async Task<Result<string>> Execute(string repoPath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet-gitversion",
                    ArgumentList = { "-output", "json" },
                    WorkingDirectory = repoPath,
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
                var message = string.IsNullOrWhiteSpace(error) ? $"dotnet-gitversion exited with code {process.ExitCode}" : error;
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
                var message = string.IsNullOrWhiteSpace(parseEx.Message) ? "Unknown error" : parseEx.Message;
                return Result.Failure<string>(message);
            }
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unknown error" : ex.Message;
            return Result.Failure<string>(message);
        }
    }

    private static async Task<Result<string>> DescribeVersion(string repoPath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    ArgumentList = { "describe", "--tags", "--long", "--match", "*.*.*" },
                    WorkingDirectory = repoPath,
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
                var message = string.IsNullOrWhiteSpace(error) ? $"git describe exited with code {process.ExitCode}" : error;
                return Result.Failure<string>(message);
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
            if (!NuGetVersion.TryParse(tag, out var baseVersion))
            {
                return Result.Failure<string>($"Invalid tag '{tag}' in git describe output");
            }

            if (!int.TryParse(parts[1], out var commits))
            {
                return Result.Failure<string>($"Invalid commit count in git describe output '{description}'");
            }

            var version = commits == 0 ? baseVersion.ToNormalizedString() : $"{baseVersion.ToNormalizedString()}-ci{commits}";

            return Result.Success(version);
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unknown error" : ex.Message;
            return Result.Failure<string>(message);
        }
    }

    private static Result<string> FindGitRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(startingDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return Result.Success(current.FullName);
            }

            current = current.Parent;
        }

        return Result.Failure<string>($"No git repository found starting from '{startingDirectory}'");
    }
}
