using System;
using CSharpFunctionalExtensions;
using System.IO;
using System.Runtime.InteropServices;
using DotnetPackaging.Publish;
using Serilog;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;
using System.Reactive.Linq;
using System.IO;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe;

public sealed class ExePackagingService
{
    private const string BrandingLogoEntry = "Branding/logo.png";
    private readonly DotnetPublisher publisher;
    private readonly ILogger logger;
    private readonly InstallerStubProvider stubProvider;

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
        : this(publisher, logger, new InstallerStubProvider(logger ?? Log.Logger))
    {
    }

    public ExePackagingService(DotnetPublisher publisher, ILogger? logger, InstallerStubProvider stubProvider)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? Log.Logger;
        this.stubProvider = stubProvider ?? throw new ArgumentNullException(nameof(stubProvider));
    }

    public Task<Result<IContainer>> BuildFromDirectory(
        IContainer publishDirectory,
        string outputName,
        Options options,
        string? vendor,
        string? runtimeIdentifier,
        IByteSource? stubFile,
        IByteSource? setupLogo)
    {
        var request = new ExePackagingRequest(
            publishDirectory,
            outputName,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            Maybe<string>.None,
            Maybe<ProjectMetadata>.None,
            ToMaybe(setupLogo));

        return Build(request);
    }

    public Task<Result<IContainer>> BuildFromDirectory(
        IContainer publishDirectory,
        string outputName,
        Options options,
        string? vendor,
        string? runtimeIdentifier,
        IByteSource? stubFile,
        IByteSource? setupLogo,
        Maybe<string> projectName,
        Maybe<ProjectMetadata> projectMetadata)
    {
        var request = new ExePackagingRequest(
            publishDirectory,
            outputName,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            projectName,
            projectMetadata,
            ToMaybe(setupLogo));

        return Build(request);
    }

    public async Task<Result<IContainer>> BuildFromProject(
        FileInfo projectFile,
        string? runtimeIdentifier,
        bool selfContained,
        string configuration,
        bool singleFile,
        bool trimmed,
        string outputName,
        Options options,
        string? vendor,
        IByteSource? stubFile,
        IByteSource? setupLogo)
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
            return Result.Failure<IContainer>(publishResult.Error);
        }

        using var pub = publishResult.Value;
        var projectMetadata = ReadProjectMetadata(projectFile);

        var request = new ExePackagingRequest(
            pub,
            outputName,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            Maybe<string>.From(Path.GetFileNameWithoutExtension(projectFile.Name)),
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

    private async Task<Result<IContainer>> Build(ExePackagingRequest request)
    {
        var inferredExecutable = InferExecutableName(request.PublishDirectory, request.ProjectName);

        var metadata = BuildInstallerMetadata(
            request.Options,
            request.PublishDirectory,
            request.Vendor,
            inferredExecutable,
            request.ProjectName,
            request.ProjectMetadata,
            request.SetupLogo);

        async Task<Result<IContainer>> BuildWithStub(IByteSource stubBytes)
        {
            var buildResult = await SimpleExePacker.Build(stubBytes, request.PublishDirectory, metadata, request.SetupLogo);
            if (buildResult.IsFailure)
            {
                return Result.Failure<IContainer>(buildResult.Error);
            }

            var resources = new List<INamedByteSource>
            {
                new Resource(request.OutputName, buildResult.Value.Installer),
            };

            return Result.Success<IContainer>(new RootContainer(resources, Enumerable.Empty<INamedContainer>()));
        }

        if (request.Stub.HasValue)
        {
            return await BuildWithStub(request.Stub.Value);
        }

        var ridResult = DetermineRuntimeIdentifier(request.RuntimeIdentifier);
        if (ridResult.IsFailure)
        {
            return Result.Failure<IContainer>(ridResult.Error);
        }

        var localStubResult = await TryResolveLocalStub(ridResult.Value);
        if (localStubResult.IsFailure)
        {
            return Result.Failure<IContainer>(localStubResult.Error);
        }

        var localStub = localStubResult.Value;
        if (localStub.HasValue)
        {
            return await BuildWithStub(localStub.Value);
        }

        var stubPathResult = await stubProvider.GetStub(ridResult.Value);
        return stubPathResult.IsSuccess
            ? await BuildWithStub(ByteSource.FromStreamFactory(() => File.OpenRead(stubPathResult.Value)))
            : Result.Failure<IContainer>(stubPathResult.Error);
    }


    private static Maybe<T> ToMaybe<T>(T? value) where T : class
    {
        return value is null ? Maybe<T>.None : Maybe<T>.From(value);
    }

    private static Maybe<string> ToMaybe(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Maybe<string>.None : Maybe<string>.From(value);
    }

    private static Result<IContainer> BuildContainer(DirectoryInfo publishDirectory)
    {
        return Result.Try<IContainer>(() =>
        {
            if (!publishDirectory.Exists)
            {
                throw new DirectoryNotFoundException($"Publish directory not found: {publishDirectory.FullName}");
            }

            return new DirectoryContainer(new DirectoryInfoWrapper(new FileSystem(), publishDirectory)).AsRoot();
        }, ex => ex.Message);
    }



    private static InstallerMetadata BuildInstallerMetadata(
        Options options,
        IContainer contextDir,
        Maybe<string> vendor,
        Maybe<string> inferredExecutable,
        Maybe<string> projectName,
        Maybe<ProjectMetadata> projectMetadata,
        Maybe<IByteSource> logoBytes)
    {
        var metadataProduct = projectMetadata
            .Bind(meta => meta.Product
                .Or(() => meta.AssemblyName)
                .Or(() => meta.AssemblyTitle));

        // Prefer explicit --application-name, then project metadata, then project name (from publish), then publish directory name
        var appName = options.Name
            .Or(() => metadataProduct)
            .Or(() => projectName)
            .GetValueOrDefault("Application"); // We can't easily get directory name from IContainer
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

    private Maybe<string> InferExecutableName(IContainer contextDir, Maybe<string> projectName)
    {
        try
        {
            var candidates = contextDir
                .ResourcesWithPathsRecursive()
                .Where(resource => resource.FullPath().Extension().Match(ext => string.Equals(ext, "exe", StringComparison.OrdinalIgnoreCase), () => false))
                .Select(path => new
                {
                    Relative = path.FullPath(),
                    Name = path.Name,
                    Stem = Path.GetFileNameWithoutExtension(path.Name)
                })
                .Where(candidate => !string.Equals(candidate.Name, "createdump.exe", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!candidates.Any())
            {
                logger.Warning("No executables were found in container when trying to infer the main executable.");
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
                    Depth = candidate.Relative.RouteFragments.Count()
                })
                .OrderBy(candidate => candidate.Depth)
                .ThenBy(candidate => candidate.Normalized.Length)
                .First();

            logger.Debug("Inferred executable '{Executable}' by selecting the shallowest candidate.", preferred.Normalized);
            return Maybe<string>.From(preferred.Normalized);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Unable to infer the executable from container.");
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

    private async Task<Result<Maybe<IByteSource>>> TryResolveLocalStub(string rid)
    {
        var projectPath = FindLocalStubProject();
        if (projectPath.HasNoValue)
        {
            return Result.Success(Maybe<IByteSource>.None);
        }

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

    private sealed record ExePackagingRequest(
        IContainer PublishDirectory,
        string OutputName,
        Options Options,
        Maybe<string> Vendor,
        Maybe<string> RuntimeIdentifier,
        Maybe<IByteSource> Stub,
        Maybe<string> ProjectName,
        Maybe<ProjectMetadata> ProjectMetadata,
        Maybe<IByteSource> SetupLogo);
}
