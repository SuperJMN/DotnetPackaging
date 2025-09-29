using FluentAssertions;
using DotnetPackaging.Deployment.Platforms.Android;

namespace DotnetPackaging.Deployment.Tests;

public class ApkNamingTests
{
    [Fact]
    public async Task Keeps_suffix_from_original_file_name()
    {
        var files = new Dictionary<string, IByteSource>
        {
            ["io.Angor.AngorApp.apk"] = ByteSource.FromString("a"),
            ["io.Angor.AngorApp-Signed.apk"] = ByteSource.FromString("b")
        };

        var container = files.ToRootContainer().Map(rc => (IContainer)rc).Value;
        var dotnet = new FakeDotnet(Result.Success(container));

        var options = new AndroidDeployment.DeploymentOptions
        {
            PackageName = "AngorApp",
            ApplicationVersion = 1,
            ApplicationDisplayVersion = "1.0.0",
            AndroidSigningKeyStore = ByteSource.FromString("dummy"),
            SigningKeyAlias = "alias",
            SigningStorePass = "store",
            SigningKeyPass = "key"
        };

        var deployment = new AndroidDeployment(dotnet, new Path("project.csproj"), options, Maybe<ILogger>.None);
        var result = await deployment.Create();

        result.Should().Succeed();
        result.Value.Select(x => x.Name).Should().BeEquivalentTo(
            "AngorApp-1.0.0-android.apk",
            "AngorApp-1.0.0-android-Signed.apk");
    }

    [Fact]
    public async Task Ignores_duplicate_final_apk_names()
    {
        var files = new Dictionary<string, IByteSource>
        {
            ["io.Angor.AngorApp.apk"] = ByteSource.FromString("a"),
            ["io.Angor.AngorApp-Signed.apk"] = ByteSource.FromString("b"),
            ["sub/io.Angor.AngorApp.apk"] = ByteSource.FromString("c"),
            ["sub/io.Angor.AngorApp-Signed.apk"] = ByteSource.FromString("d")
        };

        var container = files.ToRootContainer().Map(rc => (IContainer)rc).Value;
        var dotnet = new FakeDotnet(Result.Success(container));

        var options = new AndroidDeployment.DeploymentOptions
        {
            PackageName = "AngorApp",
            ApplicationVersion = 1,
            ApplicationDisplayVersion = "1.0.0",
            AndroidSigningKeyStore = ByteSource.FromString("dummy"),
            SigningKeyAlias = "alias",
            SigningStorePass = "store",
            SigningKeyPass = "key"
        };

        var deployment = new AndroidDeployment(dotnet, new Path("project.csproj"), options, Maybe<ILogger>.None);
        var result = await deployment.Create();

        result.Should().Succeed();
        result.Value.Select(x => x.Name).Should().BeEquivalentTo(
            "AngorApp-1.0.0-android.apk",
            "AngorApp-1.0.0-android-Signed.apk");
    }

    private class FakeDotnet(Result<IContainer> publishResult) : IDotnet
    {
        public Task<Result<IContainer>> Publish(string projectPath, string arguments = "") => Task.FromResult(publishResult);
        public Task<Result> Push(string packagePath, string apiKey) => Task.FromResult(Result.Success());
        public Task<Result<INamedByteSource>> Pack(string projectPath, string version) => Task.FromResult(Result.Failure<INamedByteSource>("Not implemented"));
    }
}
