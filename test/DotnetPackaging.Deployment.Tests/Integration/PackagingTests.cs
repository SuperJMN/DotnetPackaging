using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging.Deployment.Core;
using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Windows;
using FluentAssertions;
using Serilog;
using Xunit.Abstractions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deployment.Tests.Integration;

public class PackagingTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task TestWindows()
    {
        var dotnet = new Dotnet(new Command(Maybe<ILogger>.None), Maybe<ILogger>.None);
        
        var options = new WindowsDeployment.DeploymentOptions
        {
            Version = "1.0.0",
            PackageName = "TestApp"
        };
        
        var result = await new Packager(dotnet, Maybe<ILogger>.None)
            .CreateForWindows("/mnt/fast/Repos/SuperJMN-Zafiro/Zafiro.Avalonia/samples/TestApp/TestApp.Desktop/TestApp.Desktop.csproj", options);
        
        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Test_android()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        
        var dotnet = new Dotnet(new Command(logger), logger);
        
        var store = ByteSource.FromBytes(await File.ReadAllBytesAsync("Integration/test.keystore.b64"));
        
        var options = new AndroidDeployment.DeploymentOptions
        {
            AndroidSigningKeyStore = store,
            ApplicationDisplayVersion = "1.0",
            ApplicationVersion = 1,
            SigningKeyAlias = "android",
            SigningKeyPass = "test1234",
            SigningStorePass = "test1234",
        };
        
        var result = await new Packager(dotnet, logger)
            .CreateForAndroid("/mnt/fast/Repos/SuperJMN/angor/src/Angor/Avalonia/AngorApp.Android/AngorApp.Android.csproj", options);

        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Test_linux()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var dotnet = new Dotnet(new Command(logger), logger);
        
        var result = await new Packager(dotnet, logger)
            .CreateForLinux("/mnt/fast/Repos/SuperJMN/angor/src/Angor/Avalonia/AngorApp.Desktop/AngorApp.Desktop.csproj", new AppImageMetadata("Angor"))
            .Map(resources => resources.Select(async path => await path.WriteTo($"/home/jmn/Escritorio/{path.Name}")))
            .CombineSequentially();

        result.Should().Succeed();
    }
}