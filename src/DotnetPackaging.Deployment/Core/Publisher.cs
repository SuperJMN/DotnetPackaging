using System.Net.Http.Headers;
using System.Text.Json;
using DotnetPackaging.Deployment.Platforms.Wasm;
using DotnetPackaging.Deployment.Services.GitHub;

namespace DotnetPackaging.Deployment.Core;

public class Publisher(Context context)
{
    private Context Context { get; } = context;

    public Task<Result> PushNugetPackage(INamedByteSource file, string authToken)
    {
        var fs = new System.IO.Abstractions.FileSystem();
        return Result.Try(() => fs.Path.GetRandomFileName() + "_" + file.Name)
            .Bind(path => file.WriteTo(path).Map(() => path))
            .Bind(path => Context.Dotnet.Push(path, authToken));
    }

    public Task<Result> PublishToGitHubPages(WasmApp site, string ownerName, string repositoryName, string apiKey)
    {
        Context.Logger.Execute(x => x.Information("Publishing site to pages"));

        return GetGitHubUserInfo(apiKey)
            .Bind(userData =>
            {
                var pages = new GitHubPagesDeploymentUsingGit(site, Context, ownerName, repositoryName, apiKey, userData.User, userData.Email);
                return pages.Publish();
            });
    }
    
    private async Task<Result<(string User, string Email)>> GetGitHubUserInfo(string apiKey)
    {
        Context.Logger.Execute(x => x.Information("Extracting user info from API Key"));
        
        try
        {
            var httpClient = Context.HttpClientFactory.CreateClient("GitHub");
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Zafiro.Deployment", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await httpClient.GetAsync("https://api.github.com/user");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var user = root.GetProperty("name").GetString() ?? root.GetProperty("login").GetString() ?? "GitHub User";
            var email = root.GetProperty("email").GetString() ?? $"{root.GetProperty("login").GetString()}@users.noreply.github.com";
            
            Context.Logger.Execute(x => x.Information("Got information from API Key using GitHub's API => User: {User}. Email: {Email}", user, email));

            return Result.Success((User: user, Email: email));
        }
        catch (Exception ex)
        {
            return Result.Failure<(string name, string email)>($"Error getting GitHub user info: {ex.Message}");
        }
    }
}