namespace DotnetPackaging.Deployment.Platforms.Wasm;

public class AvaloniaSite
{
    public IContainer Contents { get; }

    private AvaloniaSite(IContainer contents)
    {
        Contents = contents;
    }
    
    public static Result<AvaloniaSite> Create(IContainer directory)
    {
        return directory.Subcontainers
            .TryFirst(d => d.Name.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)).ToResult($"Cannot find wwwroot folder in {directory}")
            .Map(wwwroot => new RootContainer(wwwroot.Resources.Append(new Resource(".nojekyll", ByteSource.FromString("No Jekyll"))), wwwroot.Subcontainers))
            .Map(toPublish => new AvaloniaSite(toPublish));
    }
}