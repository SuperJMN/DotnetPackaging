using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Services.GitHub;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Misc;
using Zafiro.Mixins;

namespace DotnetPackaging.Deployment;

public class Deployer(Context context, Packager packager, Publisher publisher)
{
    private readonly ReleasePackagingStrategy packagingStrategy = new(packager);
    public Context Context { get; } = context;

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
            return new Deployer(context, packager, publisher);
        }
    }

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
    public ReleaseBuilder CreateRelease()
    {
        return new ReleaseBuilder(Context);
    }

    // Convenience method for automatic Avalonia project discovery
    // This assumes the solution contains Avalonia projects and uses the solution path to find them
    // It means that the solution names for the projects must follow a specific pattern. Like:
    // - AvaloniaApp.Desktop (for Windows, macOS, Linux)
    // - AvaloniaApp.Android (for Android)
    // - AvaloniaApp.Browser (for WebAssembly)
    // - Avalonia.iOS (for iOS, if applicable)
    public Task<Result> CreateGitHubReleaseForAvalonia(string avaloniaSolutionPath, string version, string packageName, string appId, string appName, GitHubRepositoryConfig repositoryConfig, ReleaseData releaseData, AndroidDeployment.DeploymentOptions? androidOptions = null, TargetPlatform platforms = TargetPlatform.All)
    {
        var releaseConfig = CreateRelease()
            .WithApplicationInfo(packageName, appId, appName)
            .ForAvaloniaProjectsFromSolution(avaloniaSolutionPath, version, platforms, androidOptions)
            .Build();

        return CreateGitHubRelease(releaseConfig, repositoryConfig, releaseData);
    }
}