using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Misc;

namespace DotnetPackaging.Deployment;

public class Deployer(Context context, Packager packager, Publisher publisher)
{
    public Context Context { get; } = context;

    public Task<Result> PublishNugetPackages(IEnumerable<string> projectToPublish, string version, string nuGetApiKey)
    {
        return projectToPublish
            .Select(project => packager.CreateNugetPackage(project, version).LogInfo($"Packing {project}"))
            .CombineSequentially()
            .MapEach(resource => publisher.ToNuGet(resource, nuGetApiKey).LogInfo($"Pushing package {resource}"))
            .CombineSequentially();
    }
    
    public Task<Result> PublishAvaloniaAppToGitHubPages(string projectToPublish, string ownerName, string repositoryName, string apiKey)
    {
        Context.Logger.Execute(l => l.Information("Publishing WebAssembly application in {Project} to GitHub Pages with owner {Owner}, repository {Repository} ", projectToPublish, ownerName, repositoryName));
        return packager.CreateWasmSite(projectToPublish).LogInfo("WebAssembly application has been packaged successfully")
            .Bind(site => publisher.PublishToGitHubPages(site, ownerName, repositoryName, apiKey));
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
}