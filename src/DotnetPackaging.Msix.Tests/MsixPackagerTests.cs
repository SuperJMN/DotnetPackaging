using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using MsixPackaging.Core;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Local;
using MsixPackaging.Tests.Helpers;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using IDirectory = Zafiro.DivineBytes.IDirectory;

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
        var package = new MsixPackager(Log.Logger).Pack(new IODir(directoryInfo));
        await using (var fileStream = File.Create($"TestFiles/{folderName}/Actual.msix"))
        {
            await package.Value.DumpTo(fileStream);
        }

        var result = await MakeAppx.UnpackMsixAsync($"TestFiles/{folderName}/Actual.msix", "Unpack");
        Assert.True(result.ExitCode == 0, result.ErrorMessage + ":" + result.ErrorOutput + " - " + result.StandardOutput);
    }
}

internal class IODir(IDirectoryInfo directoryInfo) : IDirectory
{
    public string Name => directoryInfo.Name;
    public IEnumerable<INamedByteSource> Children => directoryInfo.GetFiles().Select(info => new IOFile(info));
}

internal class IOFile : INamedByteSource
{
    private readonly IFileInfo info1;

    public IOFile(IFileInfo info)
    {
        info1 = info;
        Source = ByteSource.FromStreamFactory(() => info.OpenRead(), async () => info.Length);
    }

    public IByteSource Source { get; }

    public string Name => info1.Name;

    public IObservable<byte[]> Bytes => Source;
    public Task<Maybe<long>> GetLength() => Source.GetLength();
    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return Source.Subscribe(observer);
    }
}