using System.IO.Abstractions;
using DotnetPackaging.AppImage.Kernel;
using Serilog;
using Xunit.Abstractions;

namespace DotnetPackaging.AppImage.Tests.Integration;

public class AppImageTests
{
    public AppImageTests(ITestOutputHelper testOutput)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.TestOutput(testOutput).CreateLogger();
    }
    
    [Fact]
    public async Task Integration()
    {
        var fs = new FileSystem();
        var dir = new DotnetDir(fs.DirectoryInfo.New("C:\\Users\\JMN\\Desktop\\AppDir\\AvaloniaSyncer"));
        
        var appImageResult = await AppImage.Create()
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