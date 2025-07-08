using System.IO.Abstractions;
using DotnetPackaging.Deployment.Platforms.Wasm;

namespace DotnetPackaging.Deployment.Services.GitHub;

public class GitHubPagesDeploymentUsingGit(Site site, Context context, string repositoryOwner, string repositoryName, string apiKey, string authorName, string authorEmail, string branchName = "master")
{
    public Context Context { get; } = context;
    public string RepositoryOwner { get; } = repositoryOwner;
    public string RepositoryName { get; } = repositoryName;
    public string BranchName { get; } = branchName;
    public string ApiKey { get; } = apiKey;
    public string AuthorName { get; } = authorName;
    public string AuthorEmail { get; } = authorEmail;

    private readonly FileSystem fileSystem = new();

    public Task<Result> Publish()
    {
        return CloneRepository()
            .Bind(repoDir => AddFilesToRepository(repoDir, site))
            .Bind(CommitAndPushChanges);
    }

    private Task<Result<IDirectoryInfo>> CloneRepository()
    {
        return Result.Try(() => fileSystem.Directory.CreateTempSubdirectory($"{RepositoryOwner}_{RepositoryName}"))
            .Bind(repoDir =>
            {
                var remoteUrl = $"https://github.com/{RepositoryOwner}/{RepositoryName}.git";
                return Context.Command.Execute("git", $"clone --branch {BranchName} --single-branch --depth 1 {remoteUrl} .", repoDir.FullName)
                    .Map(() => repoDir);
            });
    }

    private async Task<Result<IDirectoryInfo>> AddFilesToRepository(IDirectoryInfo repoDir, Site site)
    {
        var nojekyll = new ResourceWithPath(Path.Empty, new Resource(".nojekyll", ByteSource.FromString("No Jekyll file to disable Jekyll processing in GitHub Pages.")));
        var resources = site.Contents.ResourcesWithPathsRecursive().Append(nojekyll);
        
        foreach (var file in resources)
        {
            var targetPath = System.IO.Path.Combine(repoDir.FullName, file.FullPath().ToString());

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath)!);
            await file.WriteTo(targetPath);
        }

        // Añade los cambios al índice
        var execute = await Context.Command.Execute("git", "add .", repoDir.FullName);
        return execute.Map(() => repoDir);
    }

    private async Task<Result> CommitAndPushChanges(IDirectoryInfo repoDir)
    {
        // Configura las variables de entorno para autor y committer
        var environmentVariables = new Dictionary<string, string>
        {
            { "GIT_AUTHOR_NAME", AuthorName },
            { "GIT_AUTHOR_EMAIL", AuthorEmail },
            { "GIT_COMMITTER_NAME", AuthorName },
            { "GIT_COMMITTER_EMAIL", AuthorEmail }
        };

        // Crea un commit
        var commitCommand = $"commit --author=\"{AuthorName} <{AuthorEmail}>\" " +
                            $"-m \"Site update: {DateTime.UtcNow}\"";
        
        var commitResult = await Context.Command.Execute(
            "git",
            commitCommand,
            repoDir.FullName, 
            environmentVariables);

        // Si no hay cambios, ignora el push
        if (commitResult.IsFailure && commitResult.Error.Contains("nothing to commit"))
        {
            return Result.Success();
        }

        // Realiza el push
        return await Context.Command.Execute(
            "git", 
            $"push https://{ApiKey}@github.com/{RepositoryOwner}/{RepositoryName}.git", repoDir.FullName);
    }

}
