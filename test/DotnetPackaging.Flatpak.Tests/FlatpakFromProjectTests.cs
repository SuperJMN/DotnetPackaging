using System.Diagnostics;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace DotnetPackaging.Flatpak.Tests;

public sealed class FlatpakFromProjectTests
{
    private const string AppId = "com.example.dotnetpackaging.tool";

    [Fact]
    public async Task DotnetPackagingTool_bundle_passes_flatpak_validation()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new XunitException("Flatpak validation requires Linux.");
        }

        Assert.True(CommandExists("flatpak"), "Flatpak CLI is required to verify bundles.");
        Assert.True(CommandExists("ostree"), "ostree CLI is required to inspect bundle contents.");

        using var temp = new TempDirectory();
        var repoRoot = GetRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "src", "DotnetPackaging.Tool", "DotnetPackaging.Tool.csproj");
        var bundlePath = Path.Combine(temp.Path, "DotnetPackaging.Tool.flatpak");
        var repoPath = Path.Combine(temp.Path, "repo");
        var checkoutPath = Path.Combine(temp.Path, "checkout");

        Directory.CreateDirectory(repoPath);

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_ROLL_FORWARD"] = "Major",
        };

        await Execute(
            "dotnet",
            $"run --project \"{projectPath}\" -- flatpak from-project --project \"{projectPath}\" --output \"{bundlePath}\" --system",
            repoRoot,
            environment);

        File.Exists(bundlePath).Should().BeTrue("the CLI should produce a Flatpak bundle");

        await Execute("ostree", $"init --mode=archive-z2 --repo=\"{repoPath}\"");
        await Execute("flatpak", $"build-import-bundle \"{repoPath}\" \"{bundlePath}\"");
        await Execute("ostree", $"checkout --repo=\"{repoPath}\" app/{AppId}/x86_64/stable \"{checkoutPath}\"");

        var metadataPath = Path.Combine(checkoutPath, "metadata");
        File.Exists(metadataPath).Should().BeTrue();
        var metadata = await File.ReadAllTextAsync(metadataPath);
        metadata.Should().Contain($"name={AppId}");
        metadata.Should().Contain("runtime=org.freedesktop.Platform/x86_64/23.08");
        metadata.Should().Contain($"command={AppId}");

        var desktopPath = Path.Combine(checkoutPath, "export", "share", "applications", $"{AppId}.desktop");
        File.Exists(desktopPath).Should().BeTrue();

        var metainfoPath = Path.Combine(checkoutPath, "export", "share", "metainfo", $"{AppId}.metainfo.xml");
        File.Exists(metainfoPath).Should().BeTrue();

        var wrapperPath = Path.Combine(checkoutPath, "files", "bin", AppId);
        File.Exists(wrapperPath).Should().BeTrue();

        var executablePath = Path.Combine(checkoutPath, "files", "DotnetPackaging.Tool");
        File.Exists(executablePath).Should().BeTrue();

        var managedAssembly = Path.Combine(checkoutPath, "files", "DotnetPackaging.dll");
        File.Exists(managedAssembly).Should().BeTrue();
    }

    private static string GetRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            var solutionPath = Path.Combine(directory, "DotnetPackaging.sln");
            if (File.Exists(solutionPath))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static async Task<CommandResult> Execute(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start '{fileName}'.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdOutTask, stdErrTask, process.WaitForExitAsync());

        if (process.ExitCode != 0)
        {
            throw new XunitException(
                $"Command '{fileName} {arguments}' failed with exit code {process.ExitCode}.{Environment.NewLine}{stdOutTask.Result}{stdErrTask.Result}");
        }

        return new CommandResult(stdOutTask.Result, stdErrTask.Result);
    }

    private static bool CommandExists(string commandName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        var suffixes = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".bat", ".cmd" }
            : new[] { string.Empty };

        foreach (var path in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var suffix in suffixes)
            {
                var candidate = Path.Combine(path, commandName + suffix);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record CommandResult(string StandardOutput, string StandardError);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DotnetPackaging.Flatpak.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}
