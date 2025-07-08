using DotnetPackaging;

namespace DotnetPackaging.Deployment.Core;

public interface IDotnet
{
    public Task<Result<IContainer>> Publish(string projectPath, string arguments = "");
    Task<Result> Push(string packagePath, string apiKey);
}