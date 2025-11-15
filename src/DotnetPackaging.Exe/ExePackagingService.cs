using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using Serilog;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace DotnetPackaging.Exe;

public sealed class ExePackagingService
{
    private readonly DotnetPublisher publisher;

    public ExePackagingService()
        : this(new DotnetPublisher())
    {
    }

    public ExePackagingService(DotnetPublisher publisher)
    {
        this.publisher = publisher;
    }

    public Task<Result<FileInfo>> BuildFromDirectory(
        DirectoryInfo publishDirectory,
        FileInfo outputFile,
        Options options,
        string? vendor,
        string? runtimeIdentifier,
        FileInfo? stubFile)
    {
        var request = new ExePackagingRequest(
            publishDirectory,
            outputFile,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            Maybe<string>.None);

        return Build(request);
    }

    public async Task<Result<FileInfo>> BuildFromProject(
        FileInfo projectFile,
        string? runtimeIdentifier,
        bool selfContained,
        string configuration,
        bool singleFile,
        bool trimmed,
        FileInfo outputFile,
        Options options,
        string? vendor,
        FileInfo? stubFile)
    {
        var versionProperties = MsBuildVersionPropertiesFactory.Create(options.Version);

        var publishRequest = new ProjectPublishRequest(projectFile.FullName)
        {
            Rid = ToMaybe(runtimeIdentifier),
            SelfContained = selfContained,
            Configuration = configuration,
            SingleFile = singleFile,
            Trimmed = trimmed,
            MsBuildProperties = versionProperties
        };

        var publishResult = await publisher.Publish(publishRequest);
        if (publishResult.IsFailure)
        {
            return Result.Failure<FileInfo>(publishResult.Error);
        }

        var request = new ExePackagingRequest(
            new DirectoryInfo(publishResult.Value.OutputDirectory),
            outputFile,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            publishResult.Value.Name);

        return await Build(request);
    }

    private async Task<Result<FileInfo>> Build(ExePackagingRequest request)
    {
        var inferredExecutable = InferExecutableName(request.PublishDirectory, request.ProjectName);
        var metadata = BuildInstallerMetadata(request.Options, request.PublishDirectory, request.Vendor, inferredExecutable);

        if (request.Stub.HasValue)
        {
            var stubPath = request.Stub.Value.FullName;
            var packResult = await SimpleExePacker.Build(stubPath, request.PublishDirectory.FullName, metadata, request.Output.FullName);
            if (packResult.IsFailure)
            {
                return Result.Failure<FileInfo>(packResult.Error);
            }

            return Result.Success(request.Output);
        }

        var payloadResult = await CreateInstallerPayloadZip(request.PublishDirectory.FullName, metadata);
        if (payloadResult.IsFailure)
        {
            return Result.Failure<FileInfo>(payloadResult.Error);
        }

        var payloadPath = payloadResult.Value;
        try
        {
            var ridResult = DetermineRuntimeIdentifier(request.RuntimeIdentifier);
            if (ridResult.IsFailure)
            {
                return Result.Failure<FileInfo>(ridResult.Error);
            }

            var stubResult = await AutoPublishStubWithPayload(ridResult.Value, payloadPath);
            if (stubResult.IsFailure)
            {
                return Result.Failure<FileInfo>(stubResult.Error);
            }

            File.Copy(stubResult.Value, request.Output.FullName, true);
            return Result.Success(request.Output);
        }
        finally
        {
            TryDelete(payloadPath);
        }
    }

    private static Maybe<T> ToMaybe<T>(T? value) where T : class
    {
        return value is null ? Maybe<T>.None : Maybe<T>.From(value);
    }

    private static Maybe<string> ToMaybe(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Maybe<string>.None : Maybe<string>.From(value);
    }

    private static InstallerMetadata BuildInstallerMetadata(
        Options options,
        DirectoryInfo contextDir,
        Maybe<string> vendor,
        Maybe<string> inferredExecutable)
    {
        var appName = options.Name.GetValueOrDefault(contextDir.Name);
        var packageName = appName.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        var appId = options.Id.GetValueOrDefault($"com.{packageName}");
        var version = options.Version.GetValueOrDefault("1.0.0");
        var executable = options.ExecutableName
            .Or(() => inferredExecutable)
            .Map(NormalizeExecutableRelativePath)
            .Match(value => value, () => (string?)null);
        var effectiveVendor = vendor.Match(value => value, () => "Unknown");
        var description = options.Comment.Match(value => value, () => (string?)null);

        return new InstallerMetadata(appId, appName, version, effectiveVendor, description, executable);
    }

    private static Maybe<string> InferExecutableName(DirectoryInfo contextDir, Maybe<string> projectName)
    {
        try
        {
            var candidates = Directory
                .EnumerateFiles(contextDir.FullName, "*.exe", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Relative = Path.GetRelativePath(contextDir.FullName, path),
                    Name = Path.GetFileName(path),
                    Stem = Path.GetFileNameWithoutExtension(path)
                })
                .Where(candidate => !string.Equals(candidate.Name, "createdump.exe", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!candidates.Any())
            {
                Log.Warning("No executables were found under {Directory} when trying to infer the main executable.", contextDir.FullName);
                return Maybe<string>.None;
            }

            var byProject = projectName
                .Bind(name =>
                {
                    var match = candidates.FirstOrDefault(candidate => string.Equals(candidate.Stem, name, StringComparison.OrdinalIgnoreCase));
                    if (match is null)
                    {
                        return Maybe<string>.None;
                    }

                    var relative = NormalizeExecutableRelativePath(match.Relative);
                    Log.Information("Inferred executable '{Executable}' by matching the project name.", relative);
                    return Maybe<string>.From(relative);
                });

            if (byProject.HasValue)
            {
                return byProject;
            }

            if (candidates.Count == 1)
            {
                var relative = NormalizeExecutableRelativePath(candidates[0].Relative);
                Log.Information("Inferred executable '{Executable}' because it is the only candidate.", relative);
                return Maybe<string>.From(relative);
            }

            var preferred = candidates
                .Select(candidate => new
                {
                    candidate.Relative,
                    Normalized = NormalizeExecutableRelativePath(candidate.Relative),
                    Depth = candidate.Relative.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar)
                })
                .OrderBy(candidate => candidate.Depth)
                .ThenBy(candidate => candidate.Normalized.Length)
                .First();

            Log.Information("Inferred executable '{Executable}' by selecting the shallowest candidate.", preferred.Normalized);
            return Maybe<string>.From(preferred.Normalized);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unable to infer the executable under {Directory}.", contextDir.FullName);
            return Maybe<string>.None;
        }
    }

    private static string NormalizeExecutableRelativePath(string relative)
    {
        return relative.Replace("\\", "/");
    }

    private static Result<string> DetermineRuntimeIdentifier(Maybe<string> rid)
    {
        if (rid.HasValue)
        {
            var value = rid.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Result.Success(value);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Result.Success(RuntimeInformation.OSArchitecture == RuntimeArchitecture.Arm64 ? "win-arm64" : "win-x64");
        }

        return Result.Failure<string>("--rid is required when building EXE on non-Windows hosts (e.g., win-x64/win-arm64).");
    }

    private async Task<Result<string>> AutoPublishStubWithPayload(string rid, string payloadZip)
    {
        try
        {
            var csproj = FindStubProject();
            if (csproj is null)
            {
                return Result.Failure<string>("Could not find DotnetPackaging.Exe.Installer.csproj in repository layout.");
            }

            var request = new ProjectPublishRequest(csproj)
            {
                Rid = Maybe<string>.From(rid),
                SelfContained = true,
                Configuration = "Release",
                SingleFile = true,
                Trimmed = false,
                MsBuildProperties = new Dictionary<string, string>
                {
                    { "InstallerPayload", payloadZip },
                    { "IncludeNativeLibrariesForSelfExtract", "true" },
                    { "IncludeAllContentForSelfExtract", "true" }
                }
            };

            var publish = await publisher.Publish(request);
            if (publish.IsFailure)
            {
                return Result.Failure<string>(publish.Error);
            }

            var exe = Directory.EnumerateFiles(publish.Value.OutputDirectory, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault()
                      ?? Directory.EnumerateFiles(publish.Value.OutputDirectory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe is null)
            {
                return Result.Failure<string>("No stub .exe found after publishing with payload");
            }

            return Result.Success(exe);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    private static string? FindStubProject()
    {
        return FindFileUpwards(Environment.CurrentDirectory, Path.Combine("src", "DotnetPackaging.Exe.Installer", "DotnetPackaging.Exe.Installer.csproj"))
               ?? FindFileUpwards(AppContext.BaseDirectory, Path.Combine("..", "..", "..", "src", "DotnetPackaging.Exe.Installer", "DotnetPackaging.Exe.Installer.csproj"));
    }

    private static string? FindFileUpwards(string startDir, string relativePath)
    {
        try
        {
            var dir = new DirectoryInfo(startDir);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                dir = dir.Parent;
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<Result<string>> CreateInstallerPayloadZip(string publishDir, InstallerMetadata meta)
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "dp-exe-payload-" + Guid.NewGuid());
            Directory.CreateDirectory(tmp);
            var zipPath = Path.Combine(tmp, "payload.zip");
            await using var fs = File.Create(zipPath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

            var metaEntry = zip.CreateEntry("metadata.json", CompressionLevel.NoCompression);
            await using (var stream = metaEntry.Open())
            {
                await JsonSerializer.SerializeAsync(stream, meta, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
            }

            foreach (var file in Directory.EnumerateFiles(publishDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(publishDir, file).Replace('\\', '/');
                var entry = zip.CreateEntry($"Content/{relative}", CompressionLevel.Optimal);
                await using var src = File.OpenRead(file);
                await using var dst = entry.Open();
                await src.CopyToAsync(dst);
            }

            return Result.Success(zipPath);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
        }
    }

    private sealed record ExePackagingRequest(
        DirectoryInfo PublishDirectory,
        FileInfo Output,
        Options Options,
        Maybe<string> Vendor,
        Maybe<string> RuntimeIdentifier,
        Maybe<FileInfo> Stub,
        Maybe<string> ProjectName);
}
