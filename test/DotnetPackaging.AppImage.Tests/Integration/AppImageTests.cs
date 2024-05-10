using System.IO.Abstractions;
using Serilog;
using Xunit.Abstractions;

namespace DotnetPackaging.AppImage.Tests.Integration;

public class AppImageTests
{
    private readonly ITestOutputHelper testOutput;

    public AppImageTests(ITestOutputHelper testOutput)
    {
        this.testOutput = testOutput;
        Log.Logger = new LoggerConfiguration().WriteTo.TestOutput(testOutput).CreateLogger();
    }
    
    [Fact]
    public async Task Integration()
    {
        var fs = new FileSystem();
        var dir = new DotnetDir(fs.DirectoryInfo.New("C:\\Users\\JMN\\Desktop\\AppDir\\AvaloniaSyncer"));
        
        var appImageResult = await new DebFileBuilder(new RuntimeFactory())
            .FromDirectory(dir)
            .Configure(setup => setup
                .WithPackage("AvaloniaSyncer")
                .WithPackageId("com.SuperJMN.AvaloniaSyncer")
                .WithArchitecture(Architecture.X64)
                .AutoDetectIcon()
                .WithExecutableName("AvaloniaSyncer.Desktop"))
            .Build();

        var dumpResult = await appImageResult.Bind(image => image.ToData())
            .Bind(data => data.DumpTo("C:\\Users\\JMN\\Desktop\\File.AppImage"));
        dumpResult.Should().Succeed();
    }
}