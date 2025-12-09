using System.Diagnostics;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace DotnetPackaging.E2E.Tests;

public class PackagingTests : IDisposable
{
    private readonly TempDirectory temp;
    private readonly string repoRoot;
    private readonly string projectPath;

    public PackagingTests()
    {
        temp = new TempDirectory();
        repoRoot = GetRepositoryRoot();
        projectPath = Path.Combine(repoRoot, "test-input", "TestApp", "TestApp.csproj");
    }

    [Fact]
    public async Task Can_create_AppImage()
    {
        var output = Path.Combine(temp.Path, "TestApp.AppImage");
        await ExecutePackagingCommand("appimage", output, "--arch x64");
        File.Exists(output).Should().BeTrue();
    }

    [Fact]
    public async Task Can_create_Deb()
    {
        var output = Path.Combine(temp.Path, "TestApp.deb");
        await ExecutePackagingCommand("deb", output, "--arch x64");
        File.Exists(output).Should().BeTrue();
    }

    [Fact]
    public async Task Can_create_Rpm()
    {
        var output = Path.Combine(temp.Path, "TestApp.rpm");
        await ExecutePackagingCommand("rpm", output, "--arch x64");
        File.Exists(output).Should().BeTrue();
    }

    [Fact]
    public async Task Can_create_Flatpak()
    {
        var output = Path.Combine(temp.Path, "TestApp.flatpak");
        await ExecutePackagingCommand("flatpak", output, ""); // Flatpak auto-detects or uses default
        File.Exists(output).Should().BeTrue();
    }

    [Fact]
    public async Task Can_create_Exe()
    {
        var output = Path.Combine(temp.Path, "TestApp.exe");
        await ExecutePackagingCommand("exe", output, "--arch x64");
        File.Exists(output).Should().BeTrue();
    }

    private async Task ExecutePackagingCommand(string format, string outputPath, string extraArgs)
    {
        var toolProject = Path.Combine(repoRoot, "src", "DotnetPackaging.Tool", "DotnetPackaging.Tool.csproj");
        await Execute(
            "dotnet",
            $"run --project \"{toolProject}\" -- {format} from-project --project \"{projectPath}\" --output \"{outputPath}\" {extraArgs}",
            repoRoot);
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

    private static async Task Execute(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start '{fileName}'.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdOutTask, stdErrTask, process.WaitForExitAsync());

        if (process.ExitCode != 0)
        {
            throw new XunitException(
                $"Command '{fileName} {arguments}' failed with exit code {process.ExitCode}.{Environment.NewLine}{stdOutTask.Result}{stdErrTask.Result}");
        }
    }

    public void Dispose()
    {
        temp.Dispose();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DotnetPackaging.E2E.Tests", Guid.NewGuid().ToString("N"));
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
