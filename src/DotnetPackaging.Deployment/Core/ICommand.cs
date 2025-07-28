namespace DotnetPackaging.Deployment.Core;

public interface ICommand
{
    public Task<Result<string>> Execute(string command,
        string arguments,
        string workingDirectory = "",
        Dictionary<string, string>? environmentVariables = null);
}