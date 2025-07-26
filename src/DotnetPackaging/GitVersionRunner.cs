using System.Diagnostics;
using System.Text.Json;
using Nerdbank.GitVersioning;
using NuGet.Versioning;
using Serilog;

namespace DotnetPackaging;

public static class GitVersionRunner
{
    private static readonly string[] PreferredFields = ["NuGetPackageVersion", "NuGetPackageVersionSimple", "Version"];

    public static async Task<Result<string>> Run()
    {
        var libraryResult = RunNerdbank();
        if (libraryResult.IsSuccess)
        {
            return libraryResult;
        }

        Log.Warning("Nerdbank.GitVersioning failed: {Error}. Falling back to git describe", libraryResult.Error);
        return await DescribeVersion();
    }

    private static Result<string> RunNerdbank()
    {
        try
        {
            using var context = GitContext.Create(Environment.CurrentDirectory);
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
}
