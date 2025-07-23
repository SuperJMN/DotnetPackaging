using System.Diagnostics;
using Serilog;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    public static Task<Result<string>> Run() => GitVersionFromDescribe();

    private static async Task<Result<string>> GitVersionFromDescribe()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    ArgumentList =
                    {
                        "describe",
                        "--tags",
                        "--long",
                        "--match",
                        "*.*.*"
                    },
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
                Log.Warning("git describe failed: {Error}", message);
                return Result.Failure<string>(string.IsNullOrWhiteSpace(message) ? "Unknown error" : message);
            }

            var description = output.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                return Result.Failure<string>("git describe produced no output");
            }

            var parts = description.Split('-');
            if (parts.Length < 3)
            {
                return Result.Failure<string>($"Unexpected git describe output: {description}");
            }

            var tag = parts[0].TrimStart('v');
            if (!int.TryParse(parts[1], out var commits))
            {
                return Result.Failure<string>($"Invalid commit count in git describe output: {description}");
            }

            if (commits == 0)
            {
                return Result.Success(tag);
            }

            var versionParts = tag.Split('.');
            if (versionParts.Length != 3
                || !int.TryParse(versionParts[0], out var major)
                || !int.TryParse(versionParts[1], out var minor)
                || !int.TryParse(versionParts[2], out var patch))
            {
                return Result.Failure<string>($"Invalid tag format: {tag}");
            }

            patch++;
            var version = $"{major}.{minor}.{patch}-alpha.{commits}";
            return Result.Success(version);
        }
        catch (Exception ex)
        {
            Log.Warning("git describe invocation failed: {Message}", ex.Message);
            var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unknown error" : ex.Message;
            return Result.Failure<string>(message);
        }
    }
}
