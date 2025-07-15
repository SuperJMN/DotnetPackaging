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
using Zafiro.Misc;

namespace DotnetPackaging.Deployment.Tests.Integration;

public class PackagingTests(ITestOutputHelper outputHelper)
{
    private const string GitHubApiKey = "API-KEY";
    public static string OutputFolder = "/home/jmn/Escritorio/DotnetPackaging";
    public static string SolutionPath = "/mnt/fast/Repos/SuperJMN-Zafiro/Zafiro.Avalonia/samples/TestApp/TestApp.sln";
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
    public async Task Test_nuget_push()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var command = new Command(logger);
        var dotnet = new Dotnet(command, logger);

        var context = new Context(dotnet, command, logger, new DefaultHttpClientFactory());
        var deployer = new Deployer(context, new Packager(dotnet, logger), new Publisher(context));

        var result = await deployer.PublishNugetPackages(["PATH_TO_PROJECT"], "VERSION", GitHubApiKey);
        result.Should().Succeed();
    }
    
    [Fact]
    public async Task Create_GitHub_release()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var command = new Command(logger);
        var dotnet = new Dotnet(command, logger);

        var context = new Context(dotnet, command, logger, new DefaultHttpClientFactory());
        var deployer = new Deployer(context, new Packager(dotnet, logger), new Publisher(context));

        var releaseData = new ReleaseData("Test Release", "Tag", "BODY", isDraft: true);
        var gitHubRepositoryConfig = new GitHubRepositoryConfig("SuperJMN", "DotnetPackaging", GitHubApiKey);
        
        // Using the new builder API that supports different projects per platform
        var androidOptions = new AndroidDeployment.DeploymentOptions()
        {
            PackageName = "TestApp",
            ApplicationDisplayVersion = "1.0.0",
            ApplicationVersion = 1,
            SigningKeyAlias = "android",
            SigningKeyPass = "test1234",
            SigningStorePass = "test1234",
            AndroidSigningKeyStore = ByteSource.FromString("test.keystore")
        };

        var releaseBuilder = Deployer.CreateRelease()
            .WithVersion("1.0.0")
            .ForWindows(DesktopProject, "TestApp")
            .ForLinux(DesktopProject, "com.superjmn.testapp", "Test App", "TestApp")
            .ForAndroid(AndroidProject, androidOptions)
            .ForWebAssembly(WasmProject);
        
        var result = await deployer.CreateGitHubRelease(releaseBuilder.Build(), gitHubRepositoryConfig, releaseData);
        
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
    
    [Fact]
    public async Task Create_GitHub_release_from_solution_discovery()
    {
        var logger = new LoggerConfiguration().WriteTo.TestOutput(outputHelper).CreateLogger();
        var command = new Command(logger);
        var dotnet = new Dotnet(command, logger);

        var context = new Context(dotnet, command, logger, new DefaultHttpClientFactory());
        var deployer = new Deployer(context, new Packager(dotnet, logger), new Publisher(context));

        var releaseData = new ReleaseData("Test Release from Solution", "Tag", "BODY", isDraft: true);
        var gitHubRepositoryConfig = new GitHubRepositoryConfig("SuperJMN", "DotnetPackaging", GitHubApiKey);
        
        var androidOptions = new AndroidDeployment.DeploymentOptions()
        {
            PackageName = "TestApp",
            ApplicationDisplayVersion = "1.0.0",
            ApplicationVersion = 1,
            SigningKeyAlias = "android",
            SigningKeyPass = "test1234",
            SigningStorePass = "test1234",
            AndroidSigningKeyStore = ByteSource.FromString("test.keystore")
        };

        // Test the automatic project discovery method
        var result = await deployer.CreateAvaloniaReleaseFromSolution(
            SolutionPath, 
            "1.0.0", 
            "TestApp", 
            "com.superjmn.testapp", 
            "Test App", 
            gitHubRepositoryConfig, 
            releaseData, 
            androidOptions);
        
        result.Should().Succeed();
    }
}