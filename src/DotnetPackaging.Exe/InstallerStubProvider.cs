using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using Serilog;
using Path = System.IO.Path;
using System.Runtime.InteropServices;
using System.Reactive.Linq;
using DotnetPackaging.Publish;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

internal sealed class InstallerStubProvider(ILogger logger, IHttpClientFactory httpClientFactory, DotnetPublisher? publisher = null)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    public Task<Result<IByteSource>> ResolveStub(string rid, string? versionOverride = null)
    {
        return TryResolveLocalStub(rid)
            .Bind(maybeLocal => maybeLocal.HasValue
                ? Task.FromResult(Result.Success(maybeLocal.Value))
                : GetStub(rid, versionOverride)
                    .Map(path => ByteSource.FromStreamFactory(() => File.OpenRead(path))));
    }

    public Task<Result<string>> GetStub(string rid, string? versionOverride = null)
    {
        return DetermineVersion(versionOverride)
            .Bind(v => GetOrDownloadStub(rid, v));
    }

    private Result<StubVersion> DetermineVersion(string? versionOverride)
    {
        var raw = versionOverride;

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_VERSION");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = ReadAssemblyVersion();
        }

        return StubVersion.Parse(raw);
    }

    private static string ReadAssemblyVersion()
    {
        var asm = typeof(InstallerStubProvider).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info!.IndexOf('+');
            var ver = plus > 0 ? info.Substring(0, plus) : info;
            return ver.TrimStart('v', 'V');
        }

        var version = asm.GetName().Version;
        return version is null ? "0.0.0" : new Version(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build).ToString();
    }

    private Task<Result<string>> GetOrDownloadStub(string rid, StubVersion version)
    {
        return GetOverridePath()
            .Or(() => GetCachePath(rid, version))
            .ToResult("Stub not found locally")
            .OnFailureCompensate(() => DownloadAndCacheStub(rid, version));
    }

    private Maybe<string> GetOverridePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_PATH");
        return !string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath)
            ? Maybe<string>.From(Path.GetFullPath(overridePath))
            : Maybe<string>.None;
    }

    private Maybe<string> GetCachePath(string rid, StubVersion version)
    {
        var cacheBase = GetCacheBase();
        var cacheDir = Path.Combine(cacheBase, rid, version.AssetVersion);
        var targetPath = Path.Combine(cacheDir, "InstallerStub.exe");

        if (File.Exists(targetPath))
        {
            return Maybe<string>.From(targetPath);
        }

        return Maybe<string>.None;
    }

    private static string GetCacheBase()
    {
        var cacheBase = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_CACHE");
        return string.IsNullOrWhiteSpace(cacheBase) ? GetDefaultCacheDir() : cacheBase;
    }

    private async Task<Result<string>> DownloadAndCacheStub(string rid, StubVersion version)
    {
        var cacheBase = GetCacheBase();
        var cacheDir = Path.Combine(cacheBase, rid, version.AssetVersion);
        Directory.CreateDirectory(cacheDir);
        var targetPath = Path.Combine(cacheDir, "InstallerStub.exe");

        logger.Information("Downloading installer stub for {RID} v{Version}. This may take a while the first time. Cache: {CacheDir}", rid, version.AssetVersion, cacheDir);

        return await ResolveDownloadUrls(rid, version)
            .Bind(urls => DownloadAndVerify(urls.ExeUrl, urls.ShaUrl, version.AssetName(rid), targetPath));
    }

    private async Task<Result<(string ExeUrl, string ShaUrl)>> ResolveDownloadUrls(string rid, StubVersion version)
    {
        var configuredBase = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_URL_BASE");
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            var baseUrl = configuredBase.EndsWith('/') ? configuredBase : configuredBase + "/";
            return Result.Success((baseUrl + version.AssetName(rid), baseUrl + version.ChecksumName(rid)));
        }

        // Try probing tags
        if (version.AssetVersion != "1.0.0" && version.AssetVersion != "1.0.0.0")
        {
            foreach (var tag in version.TagCandidates())
            {
                var candidateBase = $"https://github.com/SuperJMN/DotnetPackaging/releases/download/{tag}/";
                var candidateSha = candidateBase + version.ChecksumName(rid);
                var probeResult = await TryProbeUrl(candidateSha);
                if (probeResult.IsSuccess)
                {
                    return Result.Success((candidateBase + version.AssetName(rid), candidateSha));
                }
            }
        }

        // Fallback to latest
        return await ResolveLatestRelease(rid, version)
            .Map(selection => (selection.ExeUrl, selection.ShaUrl));
    }

    private async Task<Result> TryProbeUrl(string url)
    {
        try
        {
            logger.Debug("Probing checksum: {Url}", url);
            using var client = CreateClient();
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return resp.IsSuccessStatusCode ? Result.Success() : Result.Failure("Not found");
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Probe failed for {Url}", url);
            return Result.Failure(ex.Message);
        }
    }

    private async Task<Result<string>> DownloadAndVerify(string exeUrl, string shaUrl, string assetName, string targetPath)
    {
        try
        {
            using var client = CreateClient();

            // Download SHA
            logger.Debug("Downloading checksum: {Url}", shaUrl);
            var shaTextFinal = await client.GetStringAsync(shaUrl);

            var expected = shaTextFinal
                .Trim()
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(expected))
            {
                return Result.Failure<string>("Invalid .sha256 file contents");
            }

            // Download Exe to temp
            var tmpDir = Path.Combine(Path.GetTempPath(), "dp-stub-dl-" + Guid.NewGuid());
            Directory.CreateDirectory(tmpDir);
            var tmpPath = Path.Combine(tmpDir, assetName);

            try
            {
                logger.Debug("Downloading stub: {Url}", exeUrl);
                using (var resp = await client.GetAsync(exeUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!resp.IsSuccessStatusCode)
                        return Result.Failure<string>($"Failed to download stub: {exeUrl} (HTTP {(int)resp.StatusCode})");

                    await using var stream = await resp.Content.ReadAsStreamAsync();
                    await using var outFs = File.Create(tmpPath);
                    await stream.CopyToAsync(outFs);
                }

                // Verify
                await using (var fs = File.OpenRead(tmpPath))
                {
                    using var sha = SHA256.Create();
                    var hash = await sha.ComputeHashAsync(fs);
                    var actual = Convert.ToHexString(hash);
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result.Failure<string>($"Checksum mismatch for stub. Expected {expected}, got {actual}");
                    }
                }

                // Move
                File.Move(tmpPath, targetPath, overwrite: true);
                return Result.Success(targetPath);
            }
            finally
            {
                TryDelete(tmpDir);
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    private async Task<Result<Maybe<IByteSource>>> TryResolveLocalStub(string rid)
    {
        if (publisher is null)
        {
            return Result.Success(Maybe<IByteSource>.None);
        }

        var projectPath = FindLocalStubProject();
        if (projectPath.HasNoValue)
        {
            return Result.Success(Maybe<IByteSource>.None);
        }

        logger.Information("Building installer stub locally from {ProjectPath} for {RID}.", projectPath.Value, rid);
        var publishRequest = new DotnetPackaging.Publish.ProjectPublishRequest(projectPath.Value)
        {
            Rid = Maybe<string>.From(rid),
            SelfContained = true,
            Configuration = "Release",
            SingleFile = true,
            Trimmed = false,
            MsBuildProperties = new Dictionary<string, string>
            {
                ["IncludeNativeLibrariesForSelfExtract"] = "true",
                ["IncludeAllContentForSelfExtract"] = "true",
                ["PublishTrimmed"] = "false",
                ["DebugType"] = "embedded",
                ["EnableCompressionInSingleFile"] = "true"
            }
        };

        return await publisher.Publish(publishRequest)
            .Bind(pub => ExtractStubFromPublish(pub, projectPath.Value));
    }

    private static Result<Maybe<IByteSource>> ExtractStubFromPublish(DotnetPackaging.Publish.IDisposableContainer pub, string projectPath)
    {
        using (pub)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
            var stubResource = pub.Resources
                .FirstOrDefault(r => r.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                     (string.IsNullOrWhiteSpace(assemblyName) ||
                                      Path.GetFileNameWithoutExtension(r.Name).Equals(assemblyName, StringComparison.OrdinalIgnoreCase)));

            if (stubResource == null)
            {
                // Fallback: any exe that is not createdump
                stubResource = pub.Resources
                   .FirstOrDefault(r => r.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                        !Path.GetFileName(r.Name).Equals("createdump.exe", StringComparison.OrdinalIgnoreCase));
            }

            if (stubResource == null)
            {
                return Result.Failure<Maybe<IByteSource>>($"Installer stub was published but no executable was located in the output container.");
            }

            // Ideally we would return a stream/factory directly, but IByteSource needs to be safe.
            // Since `pub` is disposed at end of block, we MUST copy data now.
            var stream = new MemoryStream();
            // sync copy for now as Bytes is likely IEnumerable
            foreach (var chunk in stubResource.Bytes.ToEnumerable())
            {
                stream.Write(chunk, 0, chunk.Length);
            }
            return Result.Success(Maybe<IByteSource>.From(ByteSource.FromBytes(stream.ToArray())));
        }
    }

    private Maybe<string> FindLocalStubProject()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var repositoryRoot = LocateRepositoryRoot(currentDirectory);
        var searchRoots = BuildSearchRoots(currentDirectory, repositoryRoot);

        foreach (var root in searchRoots)
        {
            var match = TryFindStubProjectUnder(root);
            if (match.HasValue)
            {
                return match;
            }
        }

        return Maybe<string>.None;
    }

    private IEnumerable<string> BuildSearchRoots(string currentDirectory, Maybe<string> repositoryRoot)
    {
        if (repositoryRoot.HasValue)
        {
            yield return repositoryRoot.Value;
            if (!PathsEqual(repositoryRoot.Value, currentDirectory))
            {
                yield return currentDirectory;
            }
        }
        else
        {
            yield return currentDirectory;
        }
    }

    private Maybe<string> TryFindStubProjectUnder(string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var directCandidate = Path.Combine(normalizedRoot, "src", "DotnetPackaging.Exe.Installer", "DotnetPackaging.Exe.Installer.csproj");
        if (File.Exists(directCandidate))
        {
            return Maybe<string>.From(directCandidate);
        }

        try
        {
            var match = Directory
                .EnumerateFiles(normalizedRoot, "DotnetPackaging.Exe.Installer.csproj", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();
            return match is null ? Maybe<string>.None : Maybe<string>.From(Path.GetFullPath(match));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug(ex, "Unable to enumerate DotnetPackaging.Exe.Installer.csproj under {Directory}", normalizedRoot);
            return Maybe<string>.None;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var l = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var r = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(l, r, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private Maybe<string> LocateRepositoryRoot(string startDirectory)
    {
        try
        {
            var dir = Path.GetFullPath(startDirectory);
            while (!string.IsNullOrWhiteSpace(dir))
            {
                var gitDir = Path.Combine(dir, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    return Maybe<string>.From(dir);
                }

                var parent = Directory.GetParent(dir);
                if (parent is null)
                {
                    break;
                }
                dir = parent.FullName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug(ex, "Unable to determine git repository root from {Directory}", startDirectory);
        }

        return Maybe<string>.None;
    }

    private async Task<Result<ReleaseAssetSelection>> ResolveLatestRelease(string rid, StubVersion version)
    {
        try
        {
            logger.Warning("Could not locate a release tag for v{Version}. Falling back to the latest release.", version.ReleaseTag);
            var latestApi = "https://api.github.com/repos/SuperJMN/DotnetPackaging/releases/latest";
            using var client = CreateClient();
            using var latestResp = await client.GetAsync(latestApi);
            if (!latestResp.IsSuccessStatusCode)
            {
                return Result.Failure<ReleaseAssetSelection>($"Could not locate a release tag for v{version.ReleaseTag} and failed to query latest release (HTTP {(int)latestResp.StatusCode}).");
            }

            var json = await latestResp.Content.ReadAsStringAsync();
            var latest = JsonSerializer.Deserialize<GhRelease>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (latest is null)
            {
                return Result.Failure<ReleaseAssetSelection>("Invalid response from GitHub releases API.");
            }

            var exeAsset = latest.Assets.FirstOrDefault(a => a.Name.StartsWith($"DotnetPackaging.Exe.Installer-{rid}-", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (exeAsset is null)
            {
                return Result.Failure<ReleaseAssetSelection>($"Latest release does not contain a stub asset for {rid}.");
            }

            var expectedPrefix = Path.GetFileNameWithoutExtension(exeAsset.Name);
            var shaAsset = latest.Assets.FirstOrDefault(a => a.Name.Equals(exeAsset.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
            if (shaAsset is null)
            {
                shaAsset = latest.Assets.FirstOrDefault(a => a.Name.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
            }
            if (shaAsset is null)
            {
                return Result.Failure<ReleaseAssetSelection>($"Latest release is missing checksum for asset {exeAsset.Name}.");
            }

            var assetVersion = ExtractAssetVersion(exeAsset.Name);

            return Result.Success(new ReleaseAssetSelection(exeAsset.BrowserDownloadUrl, shaAsset.BrowserDownloadUrl, assetVersion));
        }
        catch (Exception ex)
        {
            return Result.Failure<ReleaseAssetSelection>($"Could not locate a release tag for v{version.ReleaseTag} and failed to resolve latest release: {ex.Message}");
        }
    }

    private static string GetDefaultCacheDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.GetTempPath();
        return Path.Combine(baseDir, "DotnetPackaging", "stubs");
    }

    private static string? ExtractAssetVersion(string assetName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(assetName);
        var versionMarker = nameWithoutExt.LastIndexOf("-v", StringComparison.OrdinalIgnoreCase);
        if (versionMarker < 0)
        {
            return null;
        }

        var start = versionMarker + 2;
        if (start >= nameWithoutExt.Length)
        {
            return null;
        }

        return nameWithoutExt.Substring(start);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private sealed record ReleaseAssetSelection(string ExeUrl, string ShaUrl, string? AssetVersion);

    private sealed record StubVersion(string AssetVersion, string ReleaseTag)
    {
        public static Result<StubVersion> Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Result.Failure<StubVersion>("Unable to determine stub version.");
            }

            var trimmed = raw.Trim().TrimStart('v', 'V');
            if (!NuGetVersion.TryParse(trimmed, out var nugetVersion))
            {
                return Result.Failure<StubVersion>($"Invalid stub version '{raw}'.");
            }

            var assetVersion = nugetVersion.Version.ToString();
            var releaseTag = nugetVersion.ToNormalizedString();
            return Result.Success(new StubVersion(assetVersion, releaseTag));
        }

        public string AssetName(string rid) => $"DotnetPackaging.Exe.Installer-{rid}-v{AssetVersion}.exe";

        public string ChecksumName(string rid) => AssetName(rid) + ".sha256";

        public IEnumerable<string> TagCandidates()
        {
            var tags = new List<string> { $"v{ReleaseTag}" };
            if (!string.Equals(ReleaseTag, AssetVersion, StringComparison.OrdinalIgnoreCase))
            {
                tags.Add($"v{AssetVersion}");
            }

            tags.AddRange(Enumerable.Range(1, 5).Select(i => $"v{AssetVersion}-{i}"));

            return tags.Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("assets")] public List<GhAsset> Assets { get; set; } = new();
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("DotnetPackaging.Tool");
        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DotnetPackaging.Tool");
        }
        return client;
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
