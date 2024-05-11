using DotnetPackaging.AppImage.Builder;
using DotnetPackaging.AppImage.Kernel;
using Zafiro.DataModel;
using Directory = Zafiro.FileSystem.Lightweight.Directory;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task Test()
    {
        var appImageResult = await new AppImageBuilder(new RuntimeFactory())
            .FromDirectory(new Directory("AvaloniaSyncer", new List<INode>()
            {
                new File("MyExecutable",(StringData)"echo Hello"),
                new File("Content.txt", (StringData)"Content")
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