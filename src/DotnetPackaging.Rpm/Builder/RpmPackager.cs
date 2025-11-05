using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Unix;
using Zafiro.Mixins;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmPackager
{
    public static async Task<Result<FileInfo>> CreatePackage(PackageMetadata metadata, RpmLayout layout)
    {
        var rpmBuildCheck = await CheckRpmbuild();
        if (rpmBuildCheck.IsFailure)
        {
            return rpmBuildCheck.ConvertFailure<FileInfo>();
        }

        await using var tempDirectory = new TempDirectory();
        var topDir = tempDirectory.Path;
        var specsDir = Directory.CreateDirectory(Path.Combine(topDir, "SPECS"));
        var sourcesDir = Directory.CreateDirectory(Path.Combine(topDir, "SOURCES"));
        var buildDir = Directory.CreateDirectory(Path.Combine(topDir, "BUILD"));
        var srpmDir = Directory.CreateDirectory(Path.Combine(topDir, "SRPMS"));
        var rpmDir = Directory.CreateDirectory(Path.Combine(topDir, "RPMS"));
        var buildRootDir = Path.Combine(topDir, "BUILDROOT", $"{metadata.Package}-{metadata.Version}");
        Directory.CreateDirectory(buildRootDir);

        var rootFsDir = Directory.CreateDirectory(Path.Combine(sourcesDir.FullName, "rootfs"));
        var stageResult = await StageLayout(layout, rootFsDir.FullName);
        if (stageResult.IsFailure)
        {
            return Result.Failure<FileInfo>(stageResult.Error);
        }

        var specContent = BuildSpec(metadata, layout);
        var specPath = Path.Combine(specsDir.FullName, $"{metadata.Package}.spec");
        await File.WriteAllTextAsync(specPath, specContent);

        var arguments = new StringBuilder();
        arguments.Append($"-bb \"{specPath}\" ");
        arguments.Append($"--define \"_topdir {topDir}\" ");
        arguments.Append($"--define \"_rpmdir {rpmDir.FullName}\" ");
        arguments.Append($"--define \"_sourcedir {sourcesDir.FullName}\" ");
        arguments.Append($"--define \"_srcrpmdir {srpmDir.FullName}\" ");
        arguments.Append($"--define \"_builddir {buildDir.FullName}\" ");
        arguments.Append($"--buildroot \"{buildRootDir}\"");

        var runResult = await RunProcess("rpmbuild", arguments.ToString());
        if (runResult.IsFailure)
        {
            return Result.Failure<FileInfo>(runResult.Error);
        }

        var rpmFile = Directory.EnumerateFiles(rpmDir.FullName, "*.rpm", SearchOption.AllDirectories).FirstOrDefault();
        if (rpmFile == null)
        {
            return Result.Failure<FileInfo>("rpmbuild finished successfully but no RPM file was produced.");
        }

        var stableLocation = Path.Combine(Path.GetTempPath(), $"dotnetpackaging-{Guid.NewGuid():N}-{Path.GetFileName(rpmFile)}");
        File.Copy(rpmFile, stableLocation, true);

        return Result.Success(new FileInfo(stableLocation));
    }

    private static async Task<Result> StageLayout(RpmLayout layout, string rootFsDir)
    {
        try
        {
            foreach (var entry in layout.Entries)
            {
                var targetPath = Path.Combine(rootFsDir, entry.Path.TrimStart('/'));
                if (entry.Type == RpmEntryType.Directory)
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (entry.Content == null)
                {
                    return Result.Failure($"Entry '{entry.Path}' is missing content");
                }

                await using var fileStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await entry.Content.DumpTo(fileStream);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static string BuildSpec(PackageMetadata metadata, RpmLayout layout)
    {
        var summary = Sanitize(metadata.Summary.GetValueOrDefault(metadata.Comment.GetValueOrDefault(metadata.Name)));
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = metadata.Name;
        }

        var description = metadata.Description.GetValueOrDefault(metadata.Comment.GetValueOrDefault(summary));
        var license = metadata.License.GetValueOrDefault("Proprietary");
        var url = metadata.Homepage.Map(uri => uri.ToString()).GetValueOrDefault("https://example.com");
        var vendor = metadata.Maintainer.GetValueOrDefault(metadata.Name);
        var buildArch = MapArchitecture(metadata.Architecture);

        var builder = new StringBuilder();
        builder.AppendLine($"Name: {metadata.Package}");
        builder.AppendLine($"Version: {metadata.Version}");
        builder.AppendLine("Release: 1");
        builder.AppendLine($"Summary: {summary}");
        builder.AppendLine($"License: {license}");
        builder.AppendLine($"URL: {url}");
        builder.AppendLine($"Vendor: {vendor}");
        builder.AppendLine($"BuildArch: {buildArch}");
        builder.AppendLine();
        // Exclude bundled .NET runtime files under /opt/<package> from auto dependency/provides
        // This prevents rpmbuild from adding hard Requires like liblttng-ust.so.0, making the RPM more portable across RPM-based distros
        var pkgEsc = System.Text.RegularExpressions.Regex.Escape(metadata.Package);
        builder.AppendLine($"%global __requires_exclude_from ^/opt/{pkgEsc}/.*$");
        builder.AppendLine($"%global __provides_exclude_from ^/opt/{pkgEsc}/.*$");
        builder.AppendLine();
        builder.AppendLine("%description");
        builder.AppendLine(description);
        builder.AppendLine();
        builder.AppendLine("%prep");
        builder.AppendLine();
        builder.AppendLine("%build");
        builder.AppendLine();
        builder.AppendLine("%install");
        builder.AppendLine("rm -rf %{buildroot}");
        builder.AppendLine("mkdir -p %{buildroot}");
        builder.AppendLine("cp -a %{_sourcedir}/rootfs/. %{buildroot}");
        builder.AppendLine();
        builder.AppendLine("%files");
        builder.AppendLine("%defattr(-,root,root,-)");

        var directories = layout.Entries.Where(entry => entry.Type == RpmEntryType.Directory)
            .Select(entry => entry.Path)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path.Count(c => c == '/'))
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var directory in directories)
        {
            var entry = layout.Entries.First(e => e.Path == directory);
            var mode = entry.Properties.FileMode.ToFileModeString();
            var owner = entry.Properties.OwnerUsername.GetValueOrDefault("root");
            var group = entry.Properties.GroupName.GetValueOrDefault("root");
            builder.AppendLine($"%dir %attr(0{mode},{owner},{group}) {directory}");
        }

        foreach (var file in layout.Entries.Where(entry => entry.Type == RpmEntryType.File))
        {
            var mode = file.Properties.FileMode.ToFileModeString();
            var owner = file.Properties.OwnerUsername.GetValueOrDefault("root");
            var group = file.Properties.GroupName.GetValueOrDefault("root");
            builder.AppendLine($"%attr(0{mode},{owner},{group}) {file.Path}");
        }

        builder.AppendLine();
        builder.AppendLine("%changelog");
        builder.AppendLine($"* {DateTimeOffset.Now:ddd MMM dd yyyy} dotnet-packaging <support@example.com> - {metadata.Version}-1");
        builder.AppendLine("- Automated build");

        return builder.ToString();
    }

    private static string MapArchitecture(Architecture architecture)
    {
        if (architecture == Architecture.All)
        {
            return "noarch";
        }

        if (architecture == Architecture.X64)
        {
            return "x86_64";
        }

        if (architecture == Architecture.X86)
        {
            return "i386";
        }

        if (architecture == Architecture.Arm64)
        {
            return "aarch64";
        }

        if (architecture == Architecture.Arm32)
        {
            return "armv7hl";
        }

        return architecture.PackagePrefix;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('\n', ' ').Replace('\r', ' ');
    }

    private static async Task<Result> CheckRpmbuild()
    {
        try
        {
            var startInfo = new ProcessStartInfo("rpmbuild", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return Result.Failure("Unable to start rpmbuild");
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                return Result.Failure(string.IsNullOrWhiteSpace(error) ? "rpmbuild is not available" : error.Trim());
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return Result.Failure("The 'rpmbuild' tool is required to build RPM packages. Please install rpm-build.");
        }
    }

    private static async Task<Result> RunProcess(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return Result.Failure($"Unable to start process '{fileName}'");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                var message = new StringBuilder();
                message.AppendLine($"Command '{fileName} {arguments}' failed with exit code {process.ExitCode}.");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    message.AppendLine(output.Trim());
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    message.AppendLine(error.Trim());
                }

                return Result.Failure(message.ToString().Trim());
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}

internal sealed class TempDirectory : IAsyncDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dotnetpackaging-rpm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public ValueTask DisposeAsync()
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
            // Ignore cleanup errors
        }

        return ValueTask.CompletedTask;
    }
}
