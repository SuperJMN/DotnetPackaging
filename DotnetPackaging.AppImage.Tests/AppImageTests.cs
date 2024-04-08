using System.IO.Abstractions;
using System.Runtime.InteropServices;
using ClassLibrary1;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task CreateAppImage()
    {
        var stream = new MemoryStream();
        var inMemoryBlobContainer = new BlobContainer("Application", new List<IBlob>(), new List<IBlobContainer>());
        var build = new AppImageBuilder().Build(inMemoryBlobContainer, new TestRuntime(), new DefaultScriptAppRun("/Application/App.Desktop"));
        var writeResult = await build.Bind(image => AppImageWriter.Write(stream, image));
        writeResult.Should().Succeed();
    }

    [Fact]
    public async Task Integration()
    {
        var output = File.OpenWrite(@"C:\Users\JMN\Desktop\Test.AppImage");
        var fs = new FileSystem();
        var bc = new DirectoryBlobContainer("AvaloniaSyncer", fs.DirectoryInfo.New(@"C:\Users\JMN\Desktop\Testing"));
        var build = new AppImageBuilder().Build(bc, new UriRuntime(Architecture.X64), new DefaultScriptAppRun("AvaloniaSyncer/AvaloniaSyncer.Desktop"));
        var writeResult = await build.Bind(image => AppImageWriter.Write(output, image));
        writeResult.Should().Succeed();
    }

    [Fact]
    public async Task Check()
    {
        var fs = new FileSystem();
        var bc = new DirectoryBlobContainer("AvaloniaSyncer", fs.DirectoryInfo.New(@"C:\Users\JMN\Desktop\Testing"));
        await bc.GetBlobsInTree(ZafiroPath.Empty)
            .Tap(async tuples =>
            {
                var t = tuples.First(tuple => tuple.path.ToString().Equals("AvaloniaSyncer.Desktop"));
                var stream = await t.blob.StreamFactory();
                if (stream.IsSuccess)
                {
                    await using (var output = File.OpenWrite("c:\\users\\jmn\\Desktop\\AvaloniaSyncer.Desktop"))
                    {
                        await stream.Value.CopyToAsync(output);
                    }
                }

                stream.Value.Dispose();
            });
    }

    [Fact]
    public async Task Check2()
    {
        var fs = new FileSystem();
        var dir =fs.DirectoryInfo.New("C:\\Users\\JMN\\Desktop\\Testing");
        dir.CopyTo(fs.DirectoryInfo.New(@"\\wsl.localhost\Ubuntu\home\jmn\AvaloniaSyncer"));
    }
}