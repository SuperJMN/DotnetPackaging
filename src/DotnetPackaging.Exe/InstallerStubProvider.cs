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

public sealed class InstallerStubProvider
{
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    private readonly DotnetPublisher? publisher;

    public InstallerStubProvider(ILogger logger, HttpClient? httpClient = null, DotnetPublisher? publisher = null)
    {
        this.logger = logger ?? Log.Logger;
        this.httpClient = httpClient ?? new HttpClient();
        this.publisher = publisher;
        if (!this.httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotnetPackaging.Tool");
        }
    }

    public async Task<Result<IByteSource>> ResolveStub(string rid, string? versionOverride = null)
    {
        var localResult = await TryResolveLocalStub(rid);
        if (localResult.IsFailure)
        {
            return Result.Failure<IByteSource>(localResult.Error);
        }

        if (localResult.Value.HasValue)
        {
            return Result.Success(localResult.Value.Value);
        }

        var webResult = await GetStub(rid, versionOverride);
        if (webResult.IsFailure)
        {
            return Result.Failure<IByteSource>(webResult.Error);
        }

        return Result.Success<IByteSource>(ByteSource.FromStreamFactory(() => File.OpenRead(webResult.Value)));
    }

    public Task<Result<string>> GetStub(string rid, string? versionOverride = null)
    {
        var versionResult = DetermineVersion(versionOverride);
        if (versionResult.IsFailure)
        {
            return Task.FromResult(Result.Failure<string>(versionResult.Error));
        }

        return GetOrDownloadStub(rid, versionResult.Value);
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

    private async Task<Result<string>> GetOrDownloadStub(string rid, StubVersion version)
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

            var configuredBase = Environment.GetEnvironmentVariable("DOTNETPACKAGING_STUB_URL_BASE");

            var assetName = version.AssetName(rid);
            var shaName = version.ChecksumName(rid);
            var cacheDir = Path.Combine(cacheBase, rid, version.AssetVersion);
            Directory.CreateDirectory(cacheDir);
            var targetPath = Path.Combine(cacheDir, "InstallerStub.exe");
            if (File.Exists(targetPath))
            {
                return Result.Success(targetPath);
            }

            logger.Information("Downloading installer stub for {RID} v{Version}. This may take a while the first time. Cache: {CacheDir}", rid, version.AssetVersion, cacheDir);

            string? exeUrl = null;
            string? shaUrl = null;
            string? cachedShaText = null;

            if (!string.IsNullOrWhiteSpace(configuredBase))
            {
                var baseUrl = configuredBase.EndsWith('/') ? configuredBase : configuredBase + "/";
                exeUrl = baseUrl + assetName;
                shaUrl = baseUrl + shaName;
            }
            else
            {
                foreach (var tag in version.TagCandidates())
                {
                    var candidateBase = $"https://github.com/SuperJMN/DotnetPackaging/releases/download/{tag}/";
                    var candidateSha = candidateBase + shaName;
                    try
                    {
                        logger.Debug("Probing checksum: {Url}", candidateSha);
                        using var resp = await httpClient.GetAsync(candidateSha);
                        if (resp.IsSuccessStatusCode)
                        {
                            var shaTextProbe = await resp.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(shaTextProbe))
                            {
                                cachedShaText = shaTextProbe;
                                exeUrl = candidateBase + assetName;
                                shaUrl = candidateSha;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Probe failed for {Url}", candidateSha);
                    }
                }
            }

            if (exeUrl is null || shaUrl is null)
            {
                var latestResult = await ResolveLatestRelease(rid, version);
                if (latestResult.IsFailure)
                {
                    return Result.Failure<string>(latestResult.Error);
                }

                var latest = latestResult.Value;
                exeUrl = latest.ExeUrl;
                shaUrl = latest.ShaUrl;

                var effectiveVersion = latest.AssetVersion ?? version.AssetVersion;
                cacheDir = Path.Combine(cacheBase, rid, effectiveVersion);
                Directory.CreateDirectory(cacheDir);
                targetPath = Path.Combine(cacheDir, "InstallerStub.exe");
            }

            string? shaTextFinal = cachedShaText;
            if (shaTextFinal is null)
            {
                logger.Debug("Downloading checksum: {Url}", shaUrl);
                shaTextFinal = await httpClient.GetStringAsync(shaUrl!);
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
            try
            {
                await using (var outFs = File.Create(tmpPath))
                {
                    logger.Debug("Downloading stub: {Url}", exeUrl);
                    using var resp = await httpClient.GetAsync(exeUrl!, HttpCompletionOption.ResponseHeadersRead);
                    if (!resp.IsSuccessStatusCode)
                        return Result.Failure<string>($"Failed to download stub: {exeUrl} (HTTP {(int)resp.StatusCode})");
                    await using var stream = await resp.Content.ReadAsStreamAsync();
                    await stream.CopyToAsync(outFs);
                }

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

        var publishResult = await publisher.Publish(publishRequest);
        if (publishResult.IsFailure)
        {
            return Result.Failure<Maybe<IByteSource>>($"Failed to publish installer stub from {projectPath.Value}: {publishResult.Error}");
        }

        using var pub = publishResult.Value;

        var assemblyName = Path.GetFileNameWithoutExtension(projectPath.Value);
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

        var chunks = await stubResource.Bytes.ToList();

        // List<byte[]> to byte[]:
        var totalBytes = chunks.Sum(cb => cb.Length);
        var buffer = new byte[totalBytes];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, buffer, offset, chunk.Length);
            offset += chunk.Length;
        }

        return Result.Success(Maybe<IByteSource>.From(ByteSource.FromBytes(buffer)));
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
            using var latestResp = await httpClient.GetAsync(latestApi);
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

    private sealed class GhAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
