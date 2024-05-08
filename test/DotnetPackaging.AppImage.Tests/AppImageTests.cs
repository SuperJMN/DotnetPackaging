using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task Test()
    {
        var builder = await new DebFileBuilder(new RuntimeFactory())
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
    }
}

public class RuntimeFactory
{
    public IRuntime Create(Architecture architecture1)
    {
        return new FakeRuntime();
    }
}