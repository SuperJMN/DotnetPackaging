using System.Diagnostics;
using Nerdbank.GitVersioning;
using NuGet.Versioning;
using Serilog;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    public static async Task<Result<string>> Run(string? startPath = null)
    {
        var repositoryResult = FindGitRoot(startPath ?? Environment.CurrentDirectory);
        var repositoryPath = repositoryResult.GetValueOrDefault(startPath ?? Environment.CurrentDirectory);

        var libraryResult = RunNerdbank(repositoryPath);
        if (libraryResult.IsSuccess)
        {
            return libraryResult;
        }

        Log.Warning("Nerdbank.GitVersioning failed: {Error}. Falling back to git describe", libraryResult.Error);
        return await DescribeVersion(repositoryPath);
    }

    private static Result<string> RunNerdbank(string repoPath)
    {
        try
        {
            using var context = GitContext.Create(repoPath);
            var oracle = new VersionOracle(context);
            var version = oracle.NuGetPackageVersion;
            if (string.IsNullOrWhiteSpace(version))
            {
                return Result.Failure<string>("No version produced by Nerdbank.GitVersioning");
            }

            return Result.Success(version);
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
            if (!NuGetVersion.TryParse(tag, out var baseVersion))
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
