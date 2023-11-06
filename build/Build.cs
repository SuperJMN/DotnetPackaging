using System.Linq;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tooling.ProcessTasks;
using Project = Nuke.Common.ProjectModel.Project;


[AzurePipelines(AzurePipelinesImage.WindowsLatest, ImportSecrets = new[] { nameof(NuGetApiKey) }, AutoGenerate = false)]
class Build : NukeBuild
{
    [Parameter] [Secret] readonly string NuGetApiKey;

    [Solution] readonly Solution Solution;

    [Parameter("version-suffix")] public string VersionSuffix { get; set; }

    [Parameter("publish-framework")] public string PublishFramework { get; set; }

    [Parameter("publish-runtime")] public string PublishRuntime { get; set; }

    [Parameter("publish-project")] public string PublishProject { get; set; }

    [Parameter("publish-self-contained")] public bool PublishSelfContained { get; set; } = true;

    [GitVersion] readonly GitVersion GitVersion;

    [GitRepository] readonly GitRepository Repository;

    [Parameter]
    readonly Configuration Configuration = IsServerBuild
        ? Configuration.Release
        : Configuration.Debug;

    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
            var absolutePaths = RootDirectory.GlobDirectories("**/bin", "**/obj").Where(a => !((string) a).Contains("build")).ToList();
            Log.Information("Deleting {Dirs}", absolutePaths);
            absolutePaths.DeleteDirectories();
        });

    Target Pack => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var packableProjects = Solution.AllProjects.Where(x => x.GetProperty<bool>("IsPackable")).ToList();

            packableProjects.ForEach(project =>
            {
                Log.Information("Restoring workloads of {Input}", project);
                RestoreProjectWorkload(project);
            });

            DotNetPack(settings => settings
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.NuGetVersion)
                .SetOutputDirectory(OutputDirectory)
                .CombineWith(packableProjects, (packSettings, project) =>
                    packSettings.SetProject(project)));
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Requires(() => NuGetApiKey)
        .OnlyWhenStatic(() => Repository.IsOnMasterBranch())
        .Executes(() =>
        {
            DotNetNuGetPush(settings => settings
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetApiKey)
                    .CombineWith(
                        OutputDirectory.GlobFiles("*.nupkg").NotEmpty(), (s, v) => s.SetTargetPath(v)),
                degreeOfParallelism: 5, completeOnFailure: true);
        });

    public static int Main() => Execute<Build>(x => x.Publish);

    void RestoreProjectWorkload(Project project) => StartShell($@"dotnet workload restore --project {project.Path}").AssertZeroExitCode();
}