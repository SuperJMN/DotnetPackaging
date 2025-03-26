using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core;
using Zafiro.DivineBytes;
using MsixPackaging.Tests.Helpers;
using Serilog;
using Xunit;
using Xunit.Abstractions;

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

    private static async Task EnsureValid(string folderName)
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New($"TestFiles/{folderName}/Contents");
        var ioDir = new IODir(directoryInfo);
        var package = new MsixPackager(Log.Logger.AsMaybe()).Pack(ioDir);
        await using (var fileStream = File.Create($"TestFiles/{folderName}/Actual.msix"))
        {
            await package.Value.DumpTo(fileStream);
        }

        var result = await MakeAppx.UnpackMsixAsync($"TestFiles/{folderName}/Actual.msix", "Unpack");
        Assert.True(result.ExitCode == 0, result.ErrorMessage + ":" + result.ErrorOutput + " - " + result.StandardOutput);
    }
}