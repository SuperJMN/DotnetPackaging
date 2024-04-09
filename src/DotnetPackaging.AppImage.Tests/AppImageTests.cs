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
        var writeResult = await build.Bind(image => AppImageWriter.Write(stream, image));
        writeResult.Should().Succeed();
    }

    [Fact]
    public async Task Integration()
    {
        var result = await AppImage.Build(
            @"C:\Users\JMN\Desktop\Testing", 
            "AvaloniaSyncer.Desktop", 
            "C:\\Users\\JMN\\Desktop\\Test.AppImage", 
            Architecture.X64);
        result.Should().Succeed();
    }
    
    [Fact]
    public async Task FromAppDir()
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New(@"C:\Users\JMN\Desktop\AppDir");
        var appDir = new DirectoryBlobContainer(Maybe<string>.None, directoryInfo);
        var fileSystemStream = fs.File.Open("C:\\Users\\JMN\\Desktop\\Test.appimage", FileMode.Create);
        var result = await Factory.From(appDir, Architecture.X64)
            .Bind(image => AppImageWriter.Write(fileSystemStream, image));

        result.Should().Succeed();
    }
}