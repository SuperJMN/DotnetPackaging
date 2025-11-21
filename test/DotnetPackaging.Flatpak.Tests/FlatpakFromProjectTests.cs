using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
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
        using var temp = new TempDirectory();
        var repoRoot = GetRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "src", "DotnetPackaging.Tool", "DotnetPackaging.Tool.csproj");
        var bundlePath = Path.Combine(temp.Path, "DotnetPackaging.Tool.flatpak");

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_ROLL_FORWARD"] = "Major",
        };

        await Execute(
            "dotnet",
            $"run --project \"{projectPath}\" -- flatpak from-project --project \"{projectPath}\" --output \"{bundlePath}\"",
            repoRoot,
            environment);

        File.Exists(bundlePath).Should().BeTrue("the CLI should produce a Flatpak bundle");

        var objects = await ReadBundleEntries(bundlePath);

        var commitRefPath = $"refs/heads/app/{AppId}/x86_64/stable";
        objects.Should().ContainKey(commitRefPath);

        var commitChecksum = Encoding.UTF8.GetString(objects[commitRefPath]);
        var commitPath = ToObjectPath(commitChecksum);
        objects.Should().ContainKey(commitPath);

        var commit = ParseCommit(objects[commitPath]);
        var treePath = ToObjectPath(commit.TreeChecksum);
        objects.Should().ContainKey(treePath);

        var treeEntries = ParseTree(objects[treePath]);
        treeEntries.Keys.Should().Contain("metadata", "Flatpak metadata must be present");
        treeEntries.Keys.Should().Contain($"files/share/applications/{AppId}.desktop", "Desktop entry should be included");
        treeEntries.Keys.Should().Contain($"files/share/metainfo/{AppId}.metainfo.xml", "Metainfo should be included");
        treeEntries.Keys.Should().Contain($"files/bin/{AppId}", "Wrapper script should be available in bin");
        treeEntries.Keys.Should().Contain($"files/DotnetPackaging.Tool", "Published executable should be included");
        treeEntries.Keys.Should().Contain($"files/DotnetPackaging.dll", "Main assembly should be included");
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

    private static async Task<Dictionary<string, byte[]>> ReadBundleEntries(string bundlePath)
    {
        await using var stream = File.OpenRead(bundlePath);
        var reader = new TarReader(stream, leaveOpen: false);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        TarEntry? entry;
        while ((entry = await reader.GetNextEntryAsync()) is not null)
        {
            if (entry.EntryType == TarEntryType.Directory || entry.DataStream is null)
            {
                continue;
            }

            await using var ms = new MemoryStream();
            await entry.DataStream.CopyToAsync(ms);
            var name = entry.Name.StartsWith("./", StringComparison.Ordinal) ? entry.Name[2..] : entry.Name;
            entries[name] = ms.ToArray();
        }

        return entries;
    }

    private static string ToObjectPath(string checksum) => $"objects/{checksum[..2]}/{checksum[2..]}";

    private static CommitRecord ParseCommit(byte[] data)
    {
        var (treeChecksum, afterTree) = ReadNullTerminatedString(data, 0);
        var (subject, afterSubject) = ReadNullTerminatedString(data, afterTree);
        var timestamp = BitConverter.ToUInt64(data, afterSubject);
        return new CommitRecord(treeChecksum, subject, timestamp);
    }

    private static Dictionary<string, string> ParseTree(byte[] data)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < data.Length - 1)
        {
            var (name, afterName) = ReadNullTerminatedString(data, index);
            if (afterName >= data.Length)
            {
                break;
            }

            var (checksum, afterChecksum) = ReadNullTerminatedString(data, afterName);
            if (string.IsNullOrEmpty(name))
            {
                break;
            }

            entries[name] = checksum;
            index = afterChecksum;
        }

        return entries;
    }

    private static (string value, int nextIndex) ReadNullTerminatedString(byte[] buffer, int start)
    {
        var end = Array.IndexOf(buffer, (byte)0, start);
        end = end < 0 ? buffer.Length : end;
        var value = Encoding.UTF8.GetString(buffer, start, end - start);
        return (value, end + 1);
    }

    private sealed record CommandResult(string StandardOutput, string StandardError);
    private sealed record CommitRecord(string TreeChecksum, string Subject, ulong Timestamp);

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
