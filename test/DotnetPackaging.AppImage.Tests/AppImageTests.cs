using DotnetPackaging.AppImage.Builder;
using DotnetPackaging.AppImage.Core;
using FluentAssertions.Equivalency;
using Zafiro.DataModel;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task Test()
    {
        var appImageResult = await new AppImageBuilder(new RuntimeFactory())
            .Directory(new Zafiro.FileSystem.Directory("AvaloniaSyncer", new List<INode>()
            {
                new Zafiro.FileSystem.File("MyExecutable",(StringData)"echo Hello"),
                new Zafiro.FileSystem.File("Content.txt", (StringData)"Content")
            }))
            .Configure(setup => setup
                .WithPackage("AvaloniaSyncer")
                .WithPackageId("com.SuperJMN.AvaloniaSyncer")
                .WithArchitecture(Architecture.X64)
                .WithExecutableName("MyExecutable"))
            .Build();

        var dumpResult = await appImageResult.Bind(image => image.ToData())
            .Bind(data => data.DumpTo("C:\\Users\\JMN\\Desktop\\File.AppImage"));
        dumpResult.Should().Succeed();
    }
   
}