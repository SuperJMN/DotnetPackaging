using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Zafiro.FileSystem.Lightweight;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task CreateAppImage()
    {
        var stream = new MemoryStream();
        var inMemoryBlobContainer = new BlobContainer("Application", new List<IBlob>(), new List<IBlobContainer>());
        var build = new AppImageBuilder().Build(inMemoryBlobContainer, new TestRuntime(), new DefaultScriptAppRun("Application/App.Desktop"));
        var writeResult = await build.Bind(image => AppImageWriter.Write(stream, image, "Application/App.Desktop"));
        writeResult.Should().Succeed();
    }

    [Fact]
    public async Task Integration()
    {
        var output = File.OpenWrite(@"C:\Users\JMN\Desktop\Test.AppImage");
        var fs = new FileSystem();
        var bc = new DirectoryBlobContainer("AvaloniaSyncer", fs.DirectoryInfo.New(@"C:\Users\JMN\Desktop\Testing"));
        var build = new AppImageBuilder().Build(bc, new UriRuntime(Architecture.X64), new DefaultScriptAppRun("AvaloniaSyncer/AvaloniaSyncer.Desktop"));
        var writeResult = await build.Bind(image => AppImageWriter.Write(output, image, "AvaloniaSyncer/AvaloniaSyncer.Desktop"));
        writeResult.Should().Succeed();
    }
}