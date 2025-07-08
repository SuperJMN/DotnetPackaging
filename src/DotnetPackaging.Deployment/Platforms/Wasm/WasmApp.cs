namespace DotnetPackaging.Deployment.Platforms.Wasm;

public class WasmApp
{
    public IContainer Contents { get; }

    private WasmApp(IContainer contents)
    {
        Contents = contents;
    }
    
    public static Result<WasmApp> Create(IContainer publishContentsDir)
    {
        return publishContentsDir.Subcontainers
            .TryFirst(d => d.Name.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)).ToResult($"Cannot find wwwroot folder in {publishContentsDir}")
            .Map(toPublish => new WasmApp(toPublish));
    }
}