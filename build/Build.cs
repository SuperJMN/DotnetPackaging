using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.GitVersion;
using Zafiro.Deployment;

class Build : NukeBuild
{
    [Parameter] [Secret] readonly string NuGetApiKey;
    [Parameter] readonly bool Force;
    [Solution] readonly Solution Solution;
    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository Repository;
   
    Target Publish => td => td
        .Requires(() => NuGetApiKey)
        .OnlyWhenStatic(() => Repository.IsOnMainOrMasterBranch() || Force)
        .Executes(async () =>
        {
            var version = GitVersion.MajorMinorPatch;
            
            await Deployer.Instance.PublishPackages(PackableProjects, version, NuGetApiKey)
                .TapError(error => Assert.Fail(error.ToString()));
        });

    IEnumerable<string> PackableProjects =>
        Solution.AllProjects
            .Where(x => x.GetProperty<bool>("IsPackable"))
            .Where(x => !(x.Path.ToString().Contains("Test", StringComparison.InvariantCultureIgnoreCase) || x.Path.ToString().Contains("Sample", StringComparison.InvariantCultureIgnoreCase)))
            .Select(x => x.Path.ToString());

    public static int Main() => Execute<Build>(x => x.Publish);
}