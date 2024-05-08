using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task Test()
    {
        var appImageResult = await new DebFileBuilder(new FakeRuntimeFactory())
            .FromDirectory(new SlimDirectory("AvaloniaSyncer", new List<INode>()
            {
                new SlimFile("MyExecutable",(StringData)"echo Hello"),
                new SlimFile("File2.txt", (StringData)"Content")
            }))
            .Configure(setup => setup
                .WithPackage("AvaloniaSyncer")
                .WithPackageId("com.SuperJMN.AvaloniaSyncer")
                .WithArchitecture(Architecture.All)
                .WithExecutableName("MyExecutable"))
            .Build();

        var bytes = appImageResult.Map(image => image.ToData());
    }
}

public static class AppImageMixin
{
    public static IData ToData(this AppImage appImage)
    {
        return new CompositeData(appImage.Runtime);
    }
}

public class FakeRuntimeFactory
{
    public IRuntime Create(Architecture architecture)
    {
        return new FakeRuntime();
    }
}