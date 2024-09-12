using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Serilog;
using Zafiro.Mixins;
using Zafiro.Nuke;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Maybe = CSharpFunctionalExtensions.Maybe;

namespace _build;

class Build : NukeBuild
{
    [Parameter("The alias for the key in the keystore.")] readonly string AndroidSigningKeyAlias;
    [Parameter("The password of the key within the keystore file.")] [Secret] readonly string AndroidSigningKeyPass;
    [Parameter("The password for the keystore file.")] [Secret] readonly string AndroidSigningStorePass;
    [Parameter("Contents of the keystore encoded as Base64.")] readonly string Base64Keystore;
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("GitHub Authentication Token")] [Secret] readonly string GitHubAuthenticationToken;
    [Parameter("NuGet Authentication Token")] [Secret] readonly string NuGetApiKey;
    
    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository Repository;

    public AbsolutePath OutputDirectory = RootDirectory / "output";

    [Solution] Solution Solution;
    
    public static int Main() => Execute<Build>(x => x.Publish);

    protected override void OnBuildInitialized()
    {
        Actions = new Actions(Solution, Repository, RootDirectory, GitVersion, Configuration);
    }

    Actions Actions { get; set; }

    Target Clean => d => d
        .Executes(() =>
        {
            var absolutePaths = RootDirectory.GlobDirectories("**/bin", "**/obj").Where(a => !((string) a).Contains("build")).ToList();
            Log.Information("Deleting {Dirs}", absolutePaths);
            absolutePaths.DeleteDirectories();
        });

    Target RestoreWorkloads => td => td
        .Executes(() =>
        {
            DotNetWorkloadRestore(x => x.SetProject(Solution));
        });
    
    Target PublishDotNetTool => d => d
        .Requires(() => NuGetApiKey)
        .DependsOn(Clean)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            Actions.PushNuGetPackages(NuGetApiKey)
                .TapError(error => throw new ApplicationException(error));
        });

    Target PublishGui => td => td
        .DependsOn(RestoreWorkloads)
        .DependsOn(Clean)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch())
        .Requires(() => GitHubAuthenticationToken)
        .Executes(() =>
        {
            return Solution.Projects
                .TryFirst(x => x.Name.EndsWith(".Desktop"))
                .ToResult("Could not find the executable project in the solution")
                .Map(async project =>
                {
                    var windowsFiles = Task.FromResult(Actions.CreateZip(project));
                    var options = Options();
                    var linuxAppImageFiles = Actions.CreateAppImages(project, options);
            
                    var allFiles = new[] { windowsFiles, linuxAppImageFiles }.Combine();
                    return await allFiles
                        .Bind(paths => Actions.CreateGitHubRelease(GitHubAuthenticationToken, paths.Flatten().ToArray()));
                }).TapError(e => throw new ApplicationException(e)); 
        });

    Target Publish => td => td
        .DependsOn(PublishGui, PublishDotNetTool);

    Options Options()
    {
        IEnumerable<AdditionalCategory> additionalCategories = [
            AdditionalCategory.Building, 
            AdditionalCategory.Archiving,
            AdditionalCategory.GTK,
            AdditionalCategory.KDE,
        ];

        return new Options
        {
            MainCategory = MainCategory.Utility,
            AdditionalCategories = Maybe.From(additionalCategories),
            AppName = "DotnetPackagingGUI",
            Version = GitVersion.MajorMinorPatch,
            Comment = "Packager for .NET stand-alone applications, powered by AvaloniaUI",
            AppId = "com.SuperJMN.DotnetPackaging.GUI",
            StartupWmClass = "DotnetPackaging",
            HomePage = new Uri("https://github.com/SuperJMN/dotnetpackaging"),
            Keywords = new List<string>
            {
                "Installer",
                "Cross-Platform",
                "AvaloniaUI",
                "Avalonia",
                "AppImage",
                ".deb",
                ".appimage",
                "Open Source",
                "Distribute",
                "Package",
            },
            License = "MIT",
            ScreenshotUrls = Maybe<IEnumerable<Uri>>.None,
            Summary = "Lets you packager your .NET application for distributing them easily. Currently supported formats are .deb and .appimage."
        };
    }
}