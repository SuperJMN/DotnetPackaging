using DotnetPackaging.AppImage.Builder;
using DotnetPackaging.AppImage.Kernel;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task Test()
    {
        var appImageResult = await new AppImageBuilder(new RuntimeFactory())
            .FromDirectory(new SlimDirectory("AvaloniaSyncer", new List<INode>()
            {
                new SlimFile("MyExecutable",(StringData)"echo Hello"),
                new SlimFile("Content.txt", (StringData)"Content")
            }))
            .Configure(setup => setup
                .WithPackage("AvaloniaSyncer")
                .WithPackageId("com.SuperJMN.AvaloniaSyncer")
                .WithArchitecture(Architecture.X64)
                .AutoDetectIcon()
                .WithExecutableName("MyExecutable"))
            .Build();

        var dumpResult = await appImageResult.Bind(image => image.ToData())
            .Bind(data => data.DumpTo("C:\\Users\\JMN\\Desktop\\File.AppImage"));
        dumpResult.Should().Succeed();
    }
   
}