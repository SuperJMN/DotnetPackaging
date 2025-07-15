namespace DotnetPackaging.Deployment;

public class GitHubRepositoryConfig(string ownerName, string repositoryName, string apiKey)
{
    public string OwnerName { get; } = ownerName;
    public string RepositoryName { get; } = repositoryName;
    public string ApiKey { get; } = apiKey;
}