namespace DotnetPackaging.Deployment;

public interface ICommand
{
    public Task<Result> Execute(string command,
        string arguments,
        string workingDirectory = "",
        Dictionary<string, string>? environmentVariables = null);
}