using DotnetPackaging.Deployment.Services.GitHub;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Misc;
using Zafiro.Mixins;

namespace DotnetPackaging.Deployment;

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
    
    public Task<Result> CreateGitHubRelease(Func<Packager, Task<Result<IEnumerable<INamedByteSource>>>> packFiles, GitHubRepositoryConfig repositoryConfig, ReleaseData releaseData)
    {
        return packFiles(packager)
            .Bind(files => CreateGitHubRelease(files.ToList(), repositoryConfig, releaseData));
    }
}
