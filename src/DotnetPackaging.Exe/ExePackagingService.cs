using System;
using CSharpFunctionalExtensions;
using System.IO;
using System.Runtime.InteropServices;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;
using System.Linq;
using System.Reactive.Linq;
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
        : this(publisher, logger, new InstallerStubProvider(logger ?? Log.Logger, null, publisher))
    {
    }

    public ExePackagingService(DotnetPublisher publisher, ILogger? logger, InstallerStubProvider stubProvider)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? Log.Logger;
        this.stubProvider = stubProvider ?? throw new ArgumentNullException(nameof(stubProvider));
    }

    public Task<Result<IPackage>> BuildFromDirectory(
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

        return Build(request).Bind(container => ToPackage(container, outputName, null));
    }

    public Task<Result<IPackage>> BuildFromDirectory(
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

        return Build(request).Bind(container => ToPackage(container, outputName, null));
    }

    public async Task<Result<IPackage>> BuildFromProject(
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
            return Result.Failure<IPackage>(publishResult.Error);
        }

        var projectMetadata = ReadProjectMetadata(projectFile);

        var request = new ExePackagingRequest(
            publishResult.Value,
            outputName,
            options,
            ToMaybe(vendor),
            ToMaybe(runtimeIdentifier),
            ToMaybe(stubFile),
            Maybe<string>.From(Path.GetFileNameWithoutExtension(projectFile.Name)),
            projectMetadata,
            ToMaybe(setupLogo));

        var buildResult = await Build(request);
        if (buildResult.IsFailure)
        {
            publishResult.Value.Dispose();
            return Result.Failure<IPackage>(buildResult.Error);
        }

        return ToPackage(buildResult.Value, outputName, publishResult.Value);
    }

    private Maybe<ProjectMetadata> ReadProjectMetadata(FileInfo projectFile)
    {
        return ProjectMetadataReader.TryRead(projectFile, logger);
    }

    private async Task<Result<IContainer>> Build(ExePackagingRequest request)
    {
        var inferredExecutable = InferExecutableName(request.PublishDirectory, request.ProjectName);

        var primaryExecutable = request.Options.ExecutableName.Or(() => inferredExecutable);
        if (primaryExecutable.HasNoValue)
        {
            return Result.Failure<IContainer>("No executable was found in the publish directory.");
        }

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

        var stubResult = await stubProvider.ResolveStub(ridResult.Value);
        return stubResult.IsSuccess
            ? await BuildWithStub(stubResult.Value)
            : Result.Failure<IContainer>(stubResult.Error);
    }

    private static Result<IPackage> ToPackage(IContainer container, string outputName, IDisposable? cleanup)
    {
        var resource = container.Resources.FirstOrDefault();
        if (resource is null)
        {
            return Result.Failure<IPackage>("No installer artifact was produced");
        }

        var package = (IPackage)new Package(resource.Name, resource, cleanup);
        return Result.Success(package);
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
        var executableForName = options.ExecutableName
            .Or(() => inferredExecutable)
            .Map(Path.GetFileName);
        var appName = projectMetadata.HasValue
            ? ApplicationNameResolver.FromProject(options.Name, projectMetadata, executableForName.GetValueOrDefault("Application"))
            : ApplicationNameResolver.FromDirectory(options.Name, executableForName.GetValueOrDefault("Application"));
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
