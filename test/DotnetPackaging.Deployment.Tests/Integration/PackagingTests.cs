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
    public static string OutputFolder = "/home/jmn/Escritorio/DotnetPackaging";
    public static string DesktopProject = "/mnt/fast/Repos/SuperJMN-Zafiro/Zafiro.Avalonia/samples/TestApp/TestApp.Desktop/TestApp.Desktop.csproj";
    public static string AndroidProject = "/mnt/fast/Repos/SuperJMN-Zafiro/Zafiro.Avalonia/samples/TestApp/TestApp.Android/TestApp.Android.csproj";
    public static string WasmProject = "/mnt/fast/Repos/SuperJMN-Zafiro/Zafiro.Avalonia/samples/TestApp/TestApp.Browser/TestApp.Browser.csproj";
    
    [Fact]
    public async Task Test_windows()
    {
        var dotnet = new Dotnet(new Command(Maybe<ILogger>.None), Maybe<ILogger>.None);
        
        var options = new WindowsDeployment.DeploymentOptions
        {
            Version = "1.0.0",
            PackageName = "TestApp"
        };
        
        var result = await new Packager(dotnet, Maybe<ILogger>.None)
            .CreateWindowsPackages(DesktopProject, options)
            .MapEach(source => source.WriteTo(OutputFolder + "/" + source.Name))
            .CombineSequentially();
        
        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Test_android()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        
        var dotnet = new Dotnet(new Command(logger), logger);
        
        var store = ByteSource.FromBytes(await File.ReadAllBytesAsync("Integration/test.keystore"));
        
        var options = new AndroidDeployment.DeploymentOptions
        {
            PackageName = "TestApp",
            AndroidSigningKeyStore = store,
            ApplicationDisplayVersion = "1.0.0",
            ApplicationVersion = 1,
            SigningKeyAlias = "android",
            SigningKeyPass = "test1234",
            SigningStorePass = "test1234",
        };
        
        var result = await new Packager(dotnet, logger)
            .CreateAndroidPackages(AndroidProject, options)
            .MapEach(resource => resource.WriteTo(OutputFolder + "/" + resource.Name))
            .CombineSequentially();

        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Test_linux()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var dotnet = new Dotnet(new Command(logger), logger);
        
        var result = await new Packager(dotnet, logger)
            .CreateLinuxPackages(DesktopProject, new AppImageMetadata("TestApp", "Test App", "TestApp"))
            .MapEach(resource => resource.WriteTo(OutputFolder + "/" + resource.Name))
            .CombineSequentially();

        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Test_nuget_pack()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var dotnet = new Dotnet(new Command(logger), logger);
        
        var result = await new Packager(dotnet, logger)
            .CreateNugetPackage(DesktopProject, "1.0.0")
            .Bind(resource => resource.WriteTo(OutputFolder + "/" + resource.Name));

        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Test_wasm_site()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var dotnet = new Dotnet(new Command(logger), logger);

        var result = await new Packager(dotnet, logger)
            .CreateWasmSite(WasmProject)
            .Map(site => site.Contents.WriteTo(OutputFolder + "/" + "WasmSite"));

        result.Should().Succeed();
    }
}