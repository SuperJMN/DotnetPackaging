using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using DotnetPackaging.Msix.Core.Manifest;
using Zafiro.DivineBytes;
using MsixPackaging.Tests.Helpers;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using Zafiro.DivineBytes.System.IO;
using File = System.IO.File;

namespace MsixPackaging.Tests;

public class MsixPackagerTests
{
    public MsixPackagerTests(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output, outputTemplate: 
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext:l}: {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .CreateLogger();
    }
    
    [Fact]
    public async Task Minimal()
    {
        await EnsureValid("Minimal");
    }

    [Fact]
    public async Task Minimal_ValidExe()
    {
        await EnsureValid("ValidExe");
    }

    [Fact]
    public async Task Pngs()
    {
        await EnsureValid("Pngs");
    }

    [Fact]
    public async Task MinimalFull()
    {
        await EnsureValid("MinimalFull");
    }

    [Fact]
    public async Task FullAvaloniaApp()
    {
        await EnsureValid("FullAvaloniaApp");
    }
    
    [Fact]
    public async Task MinimalWithMetadata()
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New($"TestFiles/MinimalNoMetadata/Contents");
        var dir = new DirectoryContainer(directoryInfo);
        await Msix.FromDirectoryAndMetadata(dir, new AppManifestMetadata(), Maybe<ILogger>.None)
            .Map(async source =>
            {
                await using var fileStream = File.Open("TestFiles/MinimalNoMetadata/Actual.msix", FileMode.Create);
                return await source.WriteTo(fileStream);
            });
    }

    private static async Task EnsureValid(string folderName)
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New($"TestFiles/{folderName}/Contents");
        var directoryContainer = new DirectoryContainer(directoryInfo);
        
        var result = Msix.FromDirectory(directoryContainer, Log.Logger.AsMaybe());
        
        // Verify the result is successful
        Assert.True(result.IsSuccess, $"Failed to create MSIX package: {result.Error}");
        
        // Write the MSIX package to file
        await using var fileStream = File.Create($"TestFiles/{folderName}/Actual.msix");
        await result.Value.WriteTo(fileStream);
        
        // Validate the created MSIX by attempting to unpack it
        var unpackResult = await MakeAppx.UnpackMsixAsync($"TestFiles/{folderName}/Actual.msix", "Unpack");
        Assert.True(unpackResult.ExitCode == 0, 
            $"{unpackResult.ErrorMessage}: {unpackResult.ErrorOutput} - {unpackResult.StandardOutput}");
    }
}