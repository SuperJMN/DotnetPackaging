using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Zafiro.FileSystem.Lightweight;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Model;
using DotnetPackaging.Common;
using FluentAssertions;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage.Tests;

public class CustomAppImageTests
{
    //[Fact]
    //public async Task CreateAppImage()
    //{
    //    var stream = new MemoryStream();
    //    var inMemoryBlobContainer = new BlobContainer("Application", new List<IBlob>(), new List<IBlobContainer>());
    //    var build = new AppImageBuilder().Build(inMemoryBlobContainer, new TestRuntime(), new DefaultScriptAppRun("Application/App.Desktop"));
    //    var writeResult = await build.Bind(image => AppImageWriter.Write(stream, image));
    //    writeResult.Should().Succeed();
    //}
    
    [Fact]
    public async Task FromAppDir()
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New("TestFiles/AppDir/Minimal");
        var appDir = new DirectoryBlobContainer(Maybe<string>.None, directoryInfo);

        var areEqual = await new AppImageStreamComparer().AreSameAppImages(
            () =>
            {
                return Observable.FromAsync(async () =>
                {
                    var output = new MemoryStream();
                    await AppImage.FromAppDir(output, appDir, new UriRuntime(Architecture.X64));
                    output.Position = 0;
                    return output.ToObservable();
                }).SelectMany(x => x);
            }, 
            () => File.OpenRead("TestFiles/Results/Minimal-FromAppDir.appimage").ToObservable());
        
        areEqual.Should().BeTrue();
    }

    private void Dump(MemoryStream output, string path)
    {
        output.Position = 0;
        File.WriteAllBytes(path, output.ToArray());
    }

    //[Fact]
    //public async Task FromBuildDir()
    //{
    //    var fs = new FileSystem();
    //    var directoryInfo = fs.DirectoryInfo.New("TestFiles/AppDir/Minimal/AvaloniaSyncer");
    //    var appDir = new DirectoryBlobContainer(Maybe<string>.None, directoryInfo);
    //    var output = new MemoryStream();
    //    var result = await AppImage.FromBuildDir(output, appDir, arch => new TestRuntime());
    //    //Dump(output, "C:\\Users\\JMN\\Desktop\\output.appimage");
    //    var areEqual = await  new AppImageStreamComparer().AreSameAppImages(() => output, () => File.OpenRead("TestFiles/Results/Minimal-FromAppDir.appimage"));
    //    areEqual.Should().BeTrue();
    //}
}