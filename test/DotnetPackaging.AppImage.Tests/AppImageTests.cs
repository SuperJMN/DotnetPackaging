using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task Test()
    {
        var appImageResult = await new DebFileBuilder(new RuntimeFactory())
            .FromDirectory(new SlimDirectory("AvaloniaSyncer", new List<INode>()
            {
                new SlimFile("MyExecutable",(StringData)"echo Hello"),
                new SlimFile("File2.txt", (StringData)"Content")
            }))
            .Configure(setup => setup
                .WithPackage("AvaloniaSyncer")
                .WithPackageId("com.SuperJMN.AvaloniaSyncer")
                .WithArchitecture(Architecture.X64)
                .WithExecutableName("MyExecutable"))
            .Build();

        var bytes = appImageResult.Map(image => image.ToData());
    }
}