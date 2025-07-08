namespace DotnetPackaging.Deployment.Platforms.Wasm;

public class Site
{
    public IContainer Contents { get; }

    private Site(IContainer contents)
    {
        Contents = contents;
    }
    
    public static Result<Site> Create(IContainer publishContentsDir)
    {
        return publishContentsDir.Subcontainers
            .TryFirst(d => d.Name.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)).ToResult($"Cannot find wwwroot folder in {publishContentsDir}")
            .Map(toPublish => new Site(toPublish));
    }
}