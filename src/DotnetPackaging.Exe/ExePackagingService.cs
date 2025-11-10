using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Reflection;
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
        var publishRequest = new ProjectPublishRequest(projectFile.FullName)
        {
            Rid = ToMaybe(runtimeIdentifier),
            SelfContained = selfContained,
            Configuration = configuration,
            SingleFile = singleFile,
            Trimmed = trimmed
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
        var metadata = BuildInstallerMetadata(request.Options, request.PublishDirectory, request.Vendor, inferredExecutable, request.ProjectName);

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

            // Resolve stub from cache or download from GitHub Releases
            var version = GetToolVersion();
            var stubPathResult = await GetOrDownloadStub(ridResult.Value, version);
            if (stubPathResult.IsFailure)
            {
                return Result.Failure<FileInfo>(stubPathResult.Error);
            }

            var packResult = await SimpleExePacker.Build(stubPathResult.Value, request.PublishDirectory.FullName, metadata, request.Output.FullName);
            if (packResult.IsFailure)
            {
                return Result.Failure<FileInfo>(packResult.Error);
            }

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
        Maybe<string> inferredExecutable,
        Maybe<string> projectName)
    {
        // Prefer explicit --application-name, then project name (when packaging from-project), then publish directory name
        var appName = options.Name
            .Or(() => projectName)
            .GetValueOrDefault(contextDir.Name);
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

    // Legacy local-build path removed in favor of downloading prebuilt stub from GitHub Releases.
    // Kept as private method name preservation intentionally avoided to prevent accidental use.


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

    private static string GetToolVersion()
    {
        // Allow override via env var for CI/testing
        var env = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_VERSION");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env!.TrimStart('v', 'V');
        }

        // Use InformationalVersion if present, fallback to assembly version
        var asm = typeof(ExePackagingService).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip possible metadata like +sha
            var plus = info!.IndexOf('+');
            var ver = plus > 0 ? info.Substring(0, plus) : info;
            return ver.TrimStart('v', 'V');
        }
        var v = asm.GetName().Version;
        return v is null ? "0.0.0" : new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build).ToString();
    }

    private static string GetDefaultCacheDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.GetTempPath();
        return Path.Combine(baseDir, "DotnetPackaging", "stubs");
    }

    private static async Task<Result<string>> GetOrDownloadStub(string rid, string version)
    {
        try
        {
            var overridePath = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return Result.Success(Path.GetFullPath(overridePath));
            }

            var cacheBase = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_CACHE");
            if (string.IsNullOrWhiteSpace(cacheBase)) cacheBase = GetDefaultCacheDir();
            var cacheDir = Path.Combine(cacheBase, rid, version);
            Directory.CreateDirectory(cacheDir);
            var targetPath = Path.Combine(cacheDir, "InstallerStub.exe");
            if (File.Exists(targetPath))
            {
                return Result.Success(targetPath);
            }

            // Inform users that the first-time download can take longer and that the result is cached
            Log.Information("Downloading installer stub for {RID} v{Version}. This may take a while the first time. Cache: {CacheDir}", rid, version, cacheDir);

            var configuredBase = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_URL_BASE");
            var assetName = $"DotnetPackaging.Exe.Installer-{rid}-v{version}.exe";
            var shaName = assetName + ".sha256";

            using var http = new HttpClient();
            // GitHub API requires a User-Agent
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DotnetPackaging.Tool");

            // Resolve the correct release base URL. If DOTNETPACKAGING_STUB_URL_BASE is provided, use it as-is.
            // Otherwise, try tag v{version} and then v{version}-N (N=1..5) until the checksum is found.
            string? resolvedBase = null;
            string? cachedShaText = null;
            if (!string.IsNullOrWhiteSpace(configuredBase))
            {
                resolvedBase = configuredBase.EndsWith('/') ? configuredBase : configuredBase + "/";
            }
            else
            {
                var tagCandidates = new List<string> { $"v{version}" };
                tagCandidates.AddRange(Enumerable.Range(1, 5).Select(i => $"v{version}-{i}"));

                foreach (var tag in tagCandidates)
                {
                    var candidateBase = $"https://github.com/SuperJMN/DotnetPackaging/releases/download/{tag}/";
                    var candidateSha = candidateBase + shaName;
                    try
                    {
                        Log.Debug("Probing checksum: {Url}", candidateSha);
                        var resp = await http.GetAsync(candidateSha);
                        if (resp.IsSuccessStatusCode)
                        {
                            // We got the right tag – read the contents and keep the base
                            var shaTextProbe = await resp.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(shaTextProbe))
                            {
                                resolvedBase = candidateBase;
                                // stash the sha so we don't download twice
                                cachedShaText = shaTextProbe;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Probe failed for {Url}", candidateSha);
                        // ignore and try next candidate
                    }
                }
            }

            if (resolvedBase is null)
            {
                return Result.Failure<string>($"Could not locate a release tag for v{version} (tried plain tag and -1..-5)");
            }

            Log.Debug("Resolved release base: {Base}", resolvedBase);

            var exeUrl = resolvedBase + assetName;
            var shaUrl = resolvedBase + shaName;

            // If we already fetched the sha during probing, reuse it; otherwise fetch now
            string? shaTextFinal;
            if (cachedShaText is not null)
            {
                Log.Debug("Using probed checksum from {Url}", shaUrl);
                shaTextFinal = cachedShaText;
            }
            else
            {
                Log.Debug("Downloading checksum: {Url}", shaUrl);
                shaTextFinal = await http.GetStringAsync(shaUrl);
            }
            if (string.IsNullOrWhiteSpace(shaTextFinal))
            {
                return Result.Failure<string>($"Failed to download checksum: {shaUrl}");
            }
            var firstToken = shaTextFinal.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstToken))
            {
                return Result.Failure<string>("Invalid .sha256 file contents");
            }
            var expected = firstToken!.Trim();

            var tmpDir = Path.Combine(Path.GetTempPath(), "dp-stub-dl-" + Guid.NewGuid());
            Directory.CreateDirectory(tmpDir);
            var tmpPath = Path.Combine(tmpDir, assetName);
            await using (var outFs = File.Create(tmpPath))
            {
                Log.Debug("Downloading stub: {Url}", exeUrl);
                using var resp = await http.GetAsync(exeUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                    return Result.Failure<string>($"Failed to download stub: {exeUrl} (HTTP {(int)resp.StatusCode})");
                await using var stream = await resp.Content.ReadAsStreamAsync();
                await stream.CopyToAsync(outFs);
            }

            // Verify hash
            await using (var fs = File.OpenRead(tmpPath))
            {
                using var sha = SHA256.Create();
                var hash = await sha.ComputeHashAsync(fs);
                var actual = Convert.ToHexString(hash);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(tmpPath); Directory.Delete(tmpDir, true); } catch { }
                    return Result.Failure<string>($"Checksum mismatch for stub. Expected {expected}, got {actual}");
                }
            }

            // Move to cache
            File.Move(tmpPath, targetPath, overwrite: true);
            try { Directory.Delete(tmpDir, true); } catch { }
            return Result.Success(targetPath);
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
