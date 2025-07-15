using DotnetPackaging.Deployment.Services.GitHub;
using DotnetPackaging.Deployment.Platforms.Windows;
using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Linux;
using DotnetPackaging.Deployment.Core;
using DotnetPackaging.AppImage;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Misc;
using Zafiro.Mixins;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deployment;

[Flags]
public enum TargetPlatform
{
    None = 0,
    Windows = 1,
    Linux = 2,
    MacOs = 4,
    Android = 8,
    WebAssembly = 16,
    All = Windows | Linux | MacOs | Android | WebAssembly
}

public class ReleaseConfiguration
{
    public string Version { get; internal set; } = string.Empty;
    public TargetPlatform Platforms { get; internal set; } = TargetPlatform.None;
    
    // Platform-specific configurations with their own project paths
    public WindowsPlatformConfig? WindowsConfig { get; internal set; }
    public AndroidPlatformConfig? AndroidConfig { get; internal set; }
    public LinuxPlatformConfig? LinuxConfig { get; internal set; }
    public WebAssemblyPlatformConfig? WebAssemblyConfig { get; internal set; }
}

public class WindowsPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
    public WindowsDeployment.DeploymentOptions Options { get; internal set; } = null!;
}

public class AndroidPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
    public AndroidDeployment.DeploymentOptions Options { get; internal set; } = null!;
}

public class LinuxPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
    public AppImage.Metadata.AppImageMetadata Metadata { get; internal set; } = null!;
}

public class WebAssemblyPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
}

public class ReleasePackagingStrategy
{
    private readonly Packager packager;
    
    public ReleasePackagingStrategy(Packager packager)
    {
        this.packager = packager;
    }
    
    public async Task<Result<IEnumerable<INamedByteSource>>> PackageForPlatforms(ReleaseConfiguration configuration)
    {
        var allFiles = new List<INamedByteSource>();
        // var projectPath = new Path(configuration.ProjectPath);
        
        // Windows packages
        if (configuration.Platforms.HasFlag(TargetPlatform.Windows))
        {
            var windowsConfig = configuration.WindowsConfig;
            if (windowsConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "Windows deployment options are required for Windows packaging");
            }
            
            var windowsResult = await packager.CreateWindowsPackages(windowsConfig.ProjectPath, windowsConfig.Options);
            if (windowsResult.IsFailure)
                return Result.Failure<IEnumerable<INamedByteSource>>(windowsResult.Error);
            
            allFiles.AddRange(windowsResult.Value);
        }
        
        // Linux packages
        if (configuration.Platforms.HasFlag(TargetPlatform.Linux))
        {
            var linuxConfig = configuration.LinuxConfig;
            if (linuxConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "Linux metadata is required for Linux packaging. Provide AppImageMetadata with AppId, AppName, and PackageName");
            }
            
            var linuxResult = await packager.CreateLinuxPackages(linuxConfig.ProjectPath, linuxConfig.Metadata);
            if (linuxResult.IsFailure)
                return Result.Failure<IEnumerable<INamedByteSource>>(linuxResult.Error);
            
            allFiles.AddRange(linuxResult.Value);
        }
        
        // Android packages
        if (configuration.Platforms.HasFlag(TargetPlatform.Android))
        {
            var androidConfig = configuration.AndroidConfig;
            if (androidConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "Android deployment options are required for Android packaging. Includes signing keys, version codes, etc.");
            }
            
            var androidResult = await packager.CreateAndroidPackages(androidConfig.ProjectPath, androidConfig.Options);
            if (androidResult.IsFailure)
                return Result.Failure<IEnumerable<INamedByteSource>>(androidResult.Error);
            
            allFiles.AddRange(androidResult.Value);
        }
        
        // WebAssembly site
        if (configuration.Platforms.HasFlag(TargetPlatform.WebAssembly))
        {
            var wasmConfig = configuration.WebAssemblyConfig;
            if (wasmConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "WebAssembly configuration is required for WebAssembly packaging");
            }
            
            var wasmResult = await packager.CreateWasmSite(wasmConfig.ProjectPath);
            if (wasmResult.IsFailure)
                return Result.Failure<IEnumerable<INamedByteSource>>(wasmResult.Error);
            
            // Note: WasmApp is typically deployed to GitHub Pages or similar, not included as release asset
            // If you need to include WASM files in release, you'd need a conversion method
        }
        
        return Result.Success<IEnumerable<INamedByteSource>>(allFiles);
    }
}

public class ReleaseData(string releaseName, string tag, string releaseBody, bool isDraft = false, bool isPrerelease = false)
{
    public string ReleaseName { get; } = releaseName;
    public string Tag { get; } = tag;
    public string ReleaseBody { get; } = releaseBody;
    public bool IsDraft { get; } = isDraft;
    public bool IsPrerelease { get; } = isPrerelease;
}

public class GitHubRepositoryConfig(string ownerName, string repositoryName, string apiKey)
{
    public string OwnerName { get; } = ownerName;
    public string RepositoryName { get; } = repositoryName;
    public string ApiKey { get; } = apiKey;
}

public class Deployer(Context context, Packager packager, Publisher publisher)
{
    public Context Context { get; } = context;
    private readonly ReleasePackagingStrategy packagingStrategy = new(packager);

    public async Task<Result> PublishNugetPackages(IList<string> projectToPublish, string version, string nuGetApiKey)
    {
        if (projectToPublish.Any(s => string.IsNullOrWhiteSpace(s)))
        {
            return Result.Failure("One or more projects to publish are empty or null.");
        }
        
        Context.Logger.Information("Publishing projects: {@Projects}", projectToPublish);
        
        return await projectToPublish
            .Select(project => packager.CreateNugetPackage(project, version).LogInfo($"Packing {project}"))
            .CombineSequentially()
            .MapEach(resource => publisher.ToNuGet(resource, nuGetApiKey).LogInfo($"Pushing package {resource}"))
            .CombineSequentially();
    }
    
    public Task<Result> PublishAvaloniaAppToGitHubPages(string projectToPublish, GitHubRepositoryConfig repositoryConfig)
    {
        Context.Logger.Information("Publishing WebAssembly application in {Project} to GitHub Pages with owner {Owner}, repository {Repository} ", projectToPublish, repositoryConfig.OwnerName, repositoryConfig.RepositoryName);
        return packager.CreateWasmSite(projectToPublish).LogInfo("WebAssembly application has been packaged successfully")
            .Bind(site => publisher.PublishToGitHubPages(site, repositoryConfig.OwnerName, repositoryConfig.RepositoryName, repositoryConfig.ApiKey));
    }

    public static Deployer Instance
    {
        get
        {
            var logger = Maybe<ILogger>.From(Log.Logger);
            var command = new Command(logger);
            var dotnet = new Dotnet(command, logger);
            var packager = new Packager(dotnet, logger);
            var defaultHttpClientFactory = new DefaultHttpClientFactory();
            var context = new Context(dotnet, command, logger, defaultHttpClientFactory);
            var publisher = new Publisher(context);
            return new(context, packager, publisher);
        }
    }

    public async Task<Result> CreateGitHubRelease(IList<INamedByteSource> files, GitHubRepositoryConfig repositoryConfig, ReleaseData releaseData)
    {
        var releaseName = releaseData.ReleaseName;
        var tag = releaseData.Tag;
        var releaseBody = releaseData.ReleaseBody;
        var isDraft = releaseData.IsDraft;
        var isPrerelease = releaseData.IsPrerelease;
        Context.Logger.Information("Creating GitHub release with files: {@Files} for owner {Owner}, repository {Repository}", files.Select(f => f.Name), repositoryConfig.OwnerName, repositoryConfig.RepositoryName);

        var gitHubRelease = new GitHubReleaseUsingGitHubApi(Context, files, repositoryConfig.OwnerName, repositoryConfig.RepositoryName, repositoryConfig.ApiKey);
        return await gitHubRelease.CreateRelease(tag, releaseName, releaseBody, isDraft, isPrerelease)
            .TapError(error => Context.Logger.Error("Failed to create GitHub release: {Error}", error));
    }
    
    // New builder-based method for creating releases
    public Task<Result> CreateGitHubRelease(ReleaseConfiguration releaseConfig, GitHubRepositoryConfig repositoryConfig, ReleaseData releaseData)
    {
        return packagingStrategy.PackageForPlatforms(releaseConfig)
            .Bind(files => CreateGitHubRelease(files.ToList(), repositoryConfig, releaseData));
    }
    
    // Instance method to create a new builder with Context
    public ReleaseBuilder CreateRelease() => new(Context);
    
    // Convenience methods using the builder pattern
    public Task<Result> CreateDesktopRelease(string projectPath, string version, string packageName, string appId, string appName, GitHubRepositoryConfig repositoryConfig, ReleaseData releaseData)
    {
        var releaseConfig = CreateRelease()
            .WithVersion(version)
            .ForDesktop(projectPath, packageName, appId, appName)
            .Build();
            
        return CreateGitHubRelease(releaseConfig, repositoryConfig, releaseData);
    }
    
    // Convenience method for automatic Avalonia project discovery
    // This assumes the solution contains Avalonia projects and uses the solution path to find them
    // It means that the solution names for the projects must follow a specific pattern. Like:
    // - AvaloniaApp.Desktop (for Windows, macOS, Linux)
    // - AvaloniaApp.Android (for Android)
    // - AvaloniaApp.Browser (for WebAssembly)
    // - Avalonia.iOS (for iOS, if applicable)
    public Task<Result> CreateGitHubReleaseForAvalonia(string avaloniaSolutionPath, string version, string packageName, string appId, string appName, GitHubRepositoryConfig repositoryConfig, ReleaseData releaseData, AndroidDeployment.DeploymentOptions? androidOptions = null)
    {
        var releaseConfig = CreateRelease()
            .ForAvaloniaProjectsFromSolution(avaloniaSolutionPath, version, packageName, appId, appName, androidOptions)
            .Build();
            
        return CreateGitHubRelease(releaseConfig, repositoryConfig, releaseData);
    }
}
