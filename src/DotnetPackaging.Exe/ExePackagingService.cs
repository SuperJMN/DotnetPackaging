using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Reflection;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace DotnetPackaging.Exe;

public sealed class ExePackagingService
{
    private const string BrandingLogoEntry = "Branding/logo";
    private readonly DotnetPublisher publisher;
    private readonly ILogger logger;

    public ExePackagingService()
        : this(new DotnetPublisher(), Log.Logger)
    {
    }

    public ExePackagingService(ILogger? logger)
        : this(new DotnetPublisher(ToMaybe(logger)), logger)
    {
    }

    public ExePackagingService(DotnetPublisher publisher)
        : this(publisher, null)
    {
    }

    public ExePackagingService(DotnetPublisher publisher, ILogger? logger)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? Log.Logger;
    }

    public Task<Result<FileInfo>> BuildFromDirectory(
        DirectoryInfo publishDirectory,
        FileInfo outputFile,
        Options options,
        string? vendor,
        string? runtimeIdentifier,
        FileInfo? stubFile,
        FileInfo? setupLogo)
    {
        var request = new ExePackagingRequest(
            publishDirectory,
            outputFile,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            Maybe<string>.None,
            Maybe<ProjectMetadata>.None,
            ToMaybe(setupLogo));

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
        FileInfo? stubFile,
        FileInfo? setupLogo)
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

        var projectMetadata = ReadProjectMetadata(projectFile);

        var request = new ExePackagingRequest(
            new DirectoryInfo(publishResult.Value.OutputDirectory),
            outputFile,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            publishResult.Value.Name,
            projectMetadata,
            ToMaybe(setupLogo));

        return await Build(request);
    }

    private Maybe<ProjectMetadata> ReadProjectMetadata(FileInfo projectFile)
    {
        var metadataResult = ProjectMetadataReader.Read(projectFile);
        if (metadataResult.IsFailure)
        {
            logger.Warning(
                "Unable to read project metadata from {ProjectFile}: {Error}",
                projectFile.FullName,
                metadataResult.Error);
            return Maybe<ProjectMetadata>.None;
        }

        return Maybe<ProjectMetadata>.From(metadataResult.Value);
    }

    private async Task<Result<FileInfo>> Build(ExePackagingRequest request)
    {
        var inferredExecutable = InferExecutableName(request.PublishDirectory, request.ProjectName);
        var logoResult = ReadSetupLogo(request.SetupLogo);
        if (logoResult.IsFailure)
        {
            return Result.Failure<FileInfo>(logoResult.Error);
        }

        var metadata = BuildInstallerMetadata(
            request.Options,
            request.PublishDirectory,
            request.Vendor,
            inferredExecutable,
            request.ProjectName,
            request.ProjectMetadata,
            logoResult.Value);

        if (request.Stub.HasValue)
        {
            var stubPath = request.Stub.Value.FullName;
            var packResult = await SimpleExePacker.Build(stubPath, request.PublishDirectory.FullName, metadata, logoResult.Value, request.Output.FullName);
            if (packResult.IsFailure)
            {
                return Result.Failure<FileInfo>(packResult.Error);
            }

            return Result.Success(request.Output);
        }

        var payloadResult = await CreateInstallerPayloadZip(request.PublishDirectory.FullName, metadata, logoResult.Value);
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

            var localStubResult = await TryResolveLocalStub(ridResult.Value);
            if (localStubResult.IsFailure)
            {
                return Result.Failure<FileInfo>(localStubResult.Error);
            }

            var localStub = localStubResult.Value;
            if (localStub.HasValue)
            {
                var localPackResult = await SimpleExePacker.Build(localStub.Value, request.PublishDirectory.FullName, metadata, logoResult.Value, request.Output.FullName);
                if (localPackResult.IsFailure)
                {
                    return Result.Failure<FileInfo>(localPackResult.Error);
                }

                return Result.Success(request.Output);
            }

            // Resolve stub from cache or download from GitHub Releases
            var version = GetToolVersion();
            var stubPathResult = await GetOrDownloadStub(ridResult.Value, version);
            if (stubPathResult.IsFailure)
            {
                return Result.Failure<FileInfo>(stubPathResult.Error);
            }

            var packResult = await SimpleExePacker.Build(stubPathResult.Value, request.PublishDirectory.FullName, metadata, logoResult.Value, request.Output.FullName);
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

    private static Result<Maybe<byte[]>> ReadSetupLogo(Maybe<FileInfo> setupLogo)
    {
        return setupLogo.Match(
            file => Result.Try(() =>
            {
                if (!file.Exists)
                {
                    throw new FileNotFoundException($"Logo not found: {file.FullName}");
                }

                var bytes = File.ReadAllBytes(file.FullName);
                return Maybe<byte[]>.From(bytes);
            }, ex => $"Failed to read setup logo: {ex.Message}"),
            () => Result.Success(Maybe<byte[]>.None));
    }

    private static InstallerMetadata BuildInstallerMetadata(
        Options options,
        DirectoryInfo contextDir,
        Maybe<string> vendor,
        Maybe<string> inferredExecutable,
        Maybe<string> projectName,
        Maybe<ProjectMetadata> projectMetadata,
        Maybe<byte[]> logoBytes)
    {
        var metadataProduct = projectMetadata
            .Bind(meta => meta.Product
                .Or(() => meta.AssemblyName)
                .Or(() => meta.AssemblyTitle));

        // Prefer explicit --application-name, then project metadata, then project name (from publish), then publish directory name
        var appName = options.Name
            .Or(() => metadataProduct)
            .Or(() => projectName)
            .GetValueOrDefault(contextDir.Name);
        var packageName = appName.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        var appId = options.Id.GetValueOrDefault($"com.{packageName}");
        var version = options.Version.GetValueOrDefault("1.0.0");
        var executable = options.ExecutableName
            .Or(() => inferredExecutable)
            .Map(NormalizeExecutableRelativePath)
            .Match(value => value, () => (string?)null);
        var vendorFromProject = projectMetadata
            .Bind(meta => meta.Company
                .Or(() => meta.Product)
                .Or(() => meta.AssemblyName)
                .Or(() => meta.AssemblyTitle));

        var effectiveVendor = vendor
            .Or(() => vendorFromProject)
            .GetValueOrDefault("Unknown");
        var description = options.Comment.Match(value => value, () => (string?)null);

        return new InstallerMetadata(appId, appName, version, effectiveVendor, description, executable, logoBytes.HasValue);
    }

    private Maybe<string> InferExecutableName(DirectoryInfo contextDir, Maybe<string> projectName)
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
                logger.Warning("No executables were found under {Directory} when trying to infer the main executable.", contextDir.FullName);
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
                    logger.Debug("Inferred executable '{Executable}' by matching the project name.", relative);
                    return Maybe<string>.From(relative);
                });

            if (byProject.HasValue)
            {
                return byProject;
            }

            if (candidates.Count == 1)
            {
                var relative = NormalizeExecutableRelativePath(candidates[0].Relative);
                logger.Debug("Inferred executable '{Executable}' because it is the only candidate.", relative);
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

            logger.Debug("Inferred executable '{Executable}' by selecting the shallowest candidate.", preferred.Normalized);
            return Maybe<string>.From(preferred.Normalized);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Unable to infer the executable under {Directory}.", contextDir.FullName);
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

        return Result.Failure<string>("--arch is required when building EXE on non-Windows hosts (x64/arm64).");
    }

    private async Task<Result<Maybe<string>>> TryResolveLocalStub(string rid)
    {
        var projectPath = FindLocalStubProject();
        if (projectPath.HasNoValue)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-pack-service.txt"), "Local stub project NOT found.\n"); } catch { }
            return Result.Success(Maybe<string>.None);
        }

        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-pack-service.txt"), $"Local stub project found at {projectPath.Value}. Publishing for {rid}...\n"); } catch { }
        logger.Information("Building installer stub locally from {ProjectPath} for {RID}.", projectPath.Value, rid);
        var publishRequest = new ProjectPublishRequest(projectPath.Value)
        {
            Rid = ToMaybe(rid),
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

        // We need a separate publish for the uninstaller stub which must NOT be SingleFile to avoid 
        // the "AppHost with embedded payload corrupted" issue when used as a raw binary.
        // BUT, we actually want the SingleFile stub to be the installer itself.
        // The problem is we are using the SAME file for both the "Installer Stub" (wrapper) and the "Uninstaller Stub" (embedded).
        
        // If we use the SingleFile output as the embedded uninstaller, it seems to crash when extracted.
        // This might be because SingleFile apps rely on the bundle signature at the end of the file.
        // When we embed it in a zip, and then extract it, it should be identical.
        
        // However, maybe we should publish it WITHOUT SingleFile for the purpose of embedding?
        // But we want a single Uninstall.exe.
        
        // The issue is likely that the SingleFile host has issues when it was previously part of a larger bundle? No.
        
        // Let's try publishing it as a non-SingleFile app? No, then it's a folder.
        
        // What if we publish it as SingleFile but disable compression?
        // MsBuildProperties["EnableCompressionInSingleFile"] = "false";
        
        var publishResult = await publisher.Publish(publishRequest);
        if (publishResult.IsFailure)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-pack-service.txt"), $"Publish failed: {publishResult.Error}\n"); } catch { }
            return Result.Failure<Maybe<string>>($"Failed to publish installer stub from {projectPath.Value}: {publishResult.Error}");
        }

        var stubPath = ResolveStubOutputPath(projectPath.Value, publishResult.Value.OutputDirectory);
        if (stubPath.HasNoValue)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-pack-service.txt"), $"Stub output not found in {publishResult.Value.OutputDirectory}\n"); } catch { }
            return Result.Failure<Maybe<string>>($"Installer stub was published but no executable was located under {publishResult.Value.OutputDirectory}.");
        }

        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "dp-pack-service.txt"), $"Stub published to {stubPath.Value}\n"); } catch { }
        logger.Information("Using locally built installer stub at {StubPath}", stubPath.Value);
        return Result.Success(Maybe<string>.From(stubPath.Value));
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

    private static Maybe<string> ResolveStubOutputPath(string projectPath, string publishDirectory)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            var candidate = Path.Combine(publishDirectory, $"{assemblyName}.exe");
            if (File.Exists(candidate))
            {
                return Maybe<string>.From(candidate);
            }
        }

        var fallback = Directory
            .EnumerateFiles(publishDirectory, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(file => !string.Equals(Path.GetFileName(file), "createdump.exe", StringComparison.OrdinalIgnoreCase));

        return fallback is null ? Maybe<string>.None : Maybe<string>.From(fallback);
    }

    // Legacy local-build path removed in favor of downloading prebuilt stub from GitHub Releases.
    // Kept as private method name preservation intentionally avoided to prevent accidental use.


    private static async Task<Result<string>> CreateInstallerPayloadZip(string publishDir, InstallerMetadata meta, Maybe<byte[]> logoBytes)
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

            foreach (var bytes in logoBytes)
            {
                var logoEntry = zip.CreateEntry(BrandingLogoEntry, CompressionLevel.NoCompression);
                await using var logoStream = logoEntry.Open();
                await logoStream.WriteAsync(bytes, 0, bytes.Length);
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

    private async Task<Result<string>> GetOrDownloadStub(string rid, string version)
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

            // We'll first try to use the requested version. If it cannot be found, we'll fall back to the latest release.
            string chosenVersion = version;
            string assetName = $"DotnetPackaging.Exe.Installer-{rid}-v{chosenVersion}.exe";
            string shaName = assetName + ".sha256";
            string? exeUrl = null;
            string? shaUrl = null;
            string? cachedShaText = null;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DotnetPackaging.Tool");

            // 1) Check if the stub for the requested version is already cached
            var requestedVersionCacheDir = Path.Combine(cacheBase, rid, chosenVersion);
            Directory.CreateDirectory(requestedVersionCacheDir);
            var requestedTargetPath = Path.Combine(requestedVersionCacheDir, "InstallerStub.exe");
            if (File.Exists(requestedTargetPath))
            {
                return Result.Success(requestedTargetPath);
            }

            // Inform about download and cache
            logger.Information("Downloading installer stub for {RID} v{Version}. This may take a while the first time. Cache: {CacheDir}", rid, chosenVersion, requestedVersionCacheDir);

            // 2) Try to resolve URLs for the requested version
            if (!string.IsNullOrWhiteSpace(configuredBase))
            {
                var baseUrl = configuredBase.EndsWith('/') ? configuredBase : configuredBase + "/";
                exeUrl = baseUrl + assetName;
                shaUrl = baseUrl + shaName;
            }
            else
            {
                var tagCandidates = new List<string> { $"v{chosenVersion}" };
                tagCandidates.AddRange(Enumerable.Range(1, 5).Select(i => $"v{chosenVersion}-{i}"));
                foreach (var tag in tagCandidates)
                {
                    var candidateBase = $"https://github.com/SuperJMN/DotnetPackaging/releases/download/{tag}/";
                    var candidateSha = candidateBase + shaName;
                    try
                    {
                        logger.Debug("Probing checksum: {Url}", candidateSha);
                        using var resp = await http.GetAsync(candidateSha);
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

            // 3) If we couldn't resolve the requested version, fall back to the latest release via GitHub API
            if (exeUrl is null || shaUrl is null)
            {
                try
                {
                    logger.Warning("Could not locate a release tag for v{Version}. Falling back to the latest release.", chosenVersion);
                    var latestApi = "https://api.github.com/repos/SuperJMN/DotnetPackaging/releases/latest";
                    using var latestResp = await http.GetAsync(latestApi);
                    if (!latestResp.IsSuccessStatusCode)
                    {
                        return Result.Failure<string>($"Could not locate a release tag for v{version} and failed to query latest release (HTTP {(int)latestResp.StatusCode}).");
                    }
                    var json = await latestResp.Content.ReadAsStringAsync();
                    var latest = JsonSerializer.Deserialize<GhRelease>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (latest is null)
                    {
                        return Result.Failure<string>("Invalid response from GitHub releases API.");
                    }

                    // Choose the asset for the requested RID
                    var exeAsset = latest.Assets.FirstOrDefault(a => a.Name.StartsWith($"DotnetPackaging.Exe.Installer-{rid}-", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                    if (exeAsset is null)
                    {
                        return Result.Failure<string>($"Latest release does not contain a stub asset for {rid}.");
                    }

                    var expectedPrefix = Path.GetFileNameWithoutExtension(exeAsset.Name); // DotnetPackaging.Exe.Installer-{rid}-vX.Y.Z
                    var shaAsset = latest.Assets.FirstOrDefault(a => a.Name.Equals(exeAsset.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
                    if (shaAsset is null)
                    {
                        // Also try name-based match in case naming differs
                        shaAsset = latest.Assets.FirstOrDefault(a => a.Name.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
                    }
                    if (shaAsset is null)
                    {
                        return Result.Failure<string>($"Latest release is missing checksum for asset {exeAsset.Name}.");
                    }

                    exeUrl = exeAsset.BrowserDownloadUrl;
                    shaUrl = shaAsset.BrowserDownloadUrl;

                    // Derive the actual version from tag name (strip leading 'v' if present)
                    if (!string.IsNullOrWhiteSpace(latest.TagName))
                    {
                        var raw = latest.TagName.Trim();
                        chosenVersion = raw.TrimStart('v', 'V');
                    }

                    // Refresh cache dir and target path to use the chosen (latest) version
                    requestedVersionCacheDir = Path.Combine(cacheBase, rid, chosenVersion);
                    Directory.CreateDirectory(requestedVersionCacheDir);
                    requestedTargetPath = Path.Combine(requestedVersionCacheDir, "InstallerStub.exe");
                }
                catch (Exception ex)
                {
                    return Result.Failure<string>($"Could not locate a release tag for v{version} and failed to resolve latest release: {ex.Message}");
                }
            }

            // 4) Fetch checksum if we didn't already during probing
            string? shaTextFinal = cachedShaText;
            if (shaTextFinal is null)
            {
                logger.Debug("Downloading checksum: {Url}", shaUrl);
                shaTextFinal = await http.GetStringAsync(shaUrl!);
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

            // 5) Download the stub
            var tmpDir = Path.Combine(Path.GetTempPath(), "dp-stub-dl-" + Guid.NewGuid());
            Directory.CreateDirectory(tmpDir);
            var tmpPath = Path.Combine(tmpDir, assetName);
            await using (var outFs = File.Create(tmpPath))
            {
                logger.Debug("Downloading stub: {Url}", exeUrl);
                using var resp = await http.GetAsync(exeUrl!, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                    return Result.Failure<string>($"Failed to download stub: {exeUrl} (HTTP {(int)resp.StatusCode})");
                await using var stream = await resp.Content.ReadAsStreamAsync();
                await stream.CopyToAsync(outFs);
            }

            // 6) Verify checksum
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

            // 7) Move to cache (using the chosen version's directory)
            File.Move(tmpPath, requestedTargetPath, overwrite: true);
            try { Directory.Delete(tmpDir, true); } catch { }
            return Result.Success(requestedTargetPath);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
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
        Maybe<string> ProjectName,
        Maybe<ProjectMetadata> ProjectMetadata,
        Maybe<FileInfo> SetupLogo);
}
