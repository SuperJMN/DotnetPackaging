using CliCommand = System.CommandLine.Command;
using System.CommandLine;
using System.CommandLine.Invocation;
using DotnetPackaging.Deployment;
using DotnetPackaging.Deployment.Services.GitHub;
using DotnetPackaging.Deployment.Core;
using Serilog;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        var root = new RootCommand("Simple deployment tool");
        root.AddCommand(CreatePublishNugetCommand());
        root.AddCommand(CreateReleaseCommand());
        return await root.InvokeAsync(args);
    }

    static CliCommand CreatePublishNugetCommand()
    {
        var projects = new Option<List<FileInfo>>("--projects", "Projects to publish")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };
        var version = new Option<string>("--version") { IsRequired = true };
        var apiKey = new Option<string>("--api-key", () => Environment.GetEnvironmentVariable("NUGET_API_KEY") ?? string.Empty,
            "NuGet API key (or set NUGET_API_KEY env var)");

        var cmd = new CliCommand("publish-nuget", "Pack and push NuGet packages");
        cmd.AddOption(projects);
        cmd.AddOption(version);
        cmd.AddOption(apiKey);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var prj = ctx.ParseResult.GetValueForOption(projects)!;
            var ver = ctx.ParseResult.GetValueForOption(version)!;
            var key = ctx.ParseResult.GetValueForOption(apiKey)!;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("NuGet API key is required via --api-key or NUGET_API_KEY");
            }
            var result = await Deployer.Instance.PublishNugetPackages(prj.Select(p => p.FullName).ToList(), ver, key);
            if (result.IsFailure) Log.Logger.Error("{Error}", result.Error); else Log.Logger.Information("Success");
        });

        return cmd;
    }

    static CliCommand CreateReleaseCommand()
    {
        var solution = new Option<FileInfo>("--solution", "Path to solution") { IsRequired = true };
        var version = new Option<string>("--version") { IsRequired = true };
        var packageName = new Option<string>("--package-name") { IsRequired = true };
        var appId = new Option<string>("--app-id") { IsRequired = true };
        var appName = new Option<string>("--app-name") { IsRequired = true };
        var owner = new Option<string>("--owner") { IsRequired = true };
        var repository = new Option<string>("--repository") { IsRequired = true };
        var releaseName = new Option<string>("--release-name") { IsRequired = true };
        var tag = new Option<string>("--tag") { IsRequired = true };
        var body = new Option<string>("--body") { IsRequired = true };
        var draft = new Option<bool>("--draft", () => false);
        var prerelease = new Option<bool>("--prerelease", () => false);
        var platforms = new Option<string>("--platforms", () => "All", "Target platforms to package: Windows, Linux, Android, WebAssembly or combination like 'Windows, Linux'");
        var apiKey = new Option<string>("--api-key", () => Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? string.Empty,
            "GitHub API token (or set GITHUB_TOKEN)");

        var cmd = new CliCommand("create-release", "Create GitHub release using Avalonia conventions");
        cmd.AddOption(solution);
        cmd.AddOption(version);
        cmd.AddOption(packageName);
        cmd.AddOption(appId);
        cmd.AddOption(appName);
        cmd.AddOption(owner);
        cmd.AddOption(repository);
        cmd.AddOption(releaseName);
        cmd.AddOption(tag);
        cmd.AddOption(body);
        cmd.AddOption(draft);
        cmd.AddOption(prerelease);
        cmd.AddOption(platforms);
        cmd.AddOption(apiKey);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var sln = ctx.ParseResult.GetValueForOption(solution)!;
            var ver = ctx.ParseResult.GetValueForOption(version)!;
            var pkgName = ctx.ParseResult.GetValueForOption(packageName)!;
            var id = ctx.ParseResult.GetValueForOption(appId)!;
            var name = ctx.ParseResult.GetValueForOption(appName)!;
            var ownerVal = ctx.ParseResult.GetValueForOption(owner)!;
            var repoVal = ctx.ParseResult.GetValueForOption(repository)!;
            var relName = ctx.ParseResult.GetValueForOption(releaseName)!;
            var tagVal = ctx.ParseResult.GetValueForOption(tag)!;
            var bodyVal = ctx.ParseResult.GetValueForOption(body)!;
            var isDraft = ctx.ParseResult.GetValueForOption(draft);
            var isPrerelease = ctx.ParseResult.GetValueForOption(prerelease);
            var platformsVal = ctx.ParseResult.GetValueForOption(platforms)!;
            var token = ctx.ParseResult.GetValueForOption(apiKey)!;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("GitHub token is required via --api-key or GITHUB_TOKEN");
            }
            var deployer = Deployer.Instance;
            var repoConfig = new GitHubRepositoryConfig(ownerVal, repoVal, token);
            var releaseData = new ReleaseData(relName, tagVal, bodyVal, isDraft, isPrerelease);
            if (!Enum.TryParse<TargetPlatform>(platformsVal.Replace('|', ','), true, out var platformFlags))
            {
                throw new InvalidOperationException($"Invalid platforms value: {platformsVal}");
            }
            var result = await deployer.CreateGitHubReleaseForAvalonia(sln.FullName, ver, pkgName, id, name, repoConfig, releaseData, null, platformFlags);
            if (result.IsFailure) Log.Logger.Error("{Error}", result.Error); else Log.Logger.Information("Success");
        });

        return cmd;
    }
}
