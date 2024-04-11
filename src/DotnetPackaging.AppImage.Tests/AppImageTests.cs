using System.IO.Abstractions;
using System.Runtime.InteropServices;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Reactive;
using File = System.IO.File;

namespace DotnetPackaging.AppImage.Tests;

public class CustomAppImageTests
{
    [Fact]
    public async Task Minimal_from_app_dir()
    {
        var streamGenerator = StreamGenerator.Generate(stream =>
        {
            var fs = new FileSystem();
            var directoryInfo = fs.DirectoryInfo.New("TestFiles/AppDir/Minimal");
            var appDir = new DirectorioIODirectory(Maybe<string>.None, directoryInfo);
            return AppImage.FromAppDir(stream, appDir, new UriRuntime(Architecture.X64));
        });

        var result = await streamGenerator().Map(streamFactory => AreEqual(
            streamFactory,
            () => File.OpenRead("TestFiles/Results/Minimal-FromAppDir.appimage")));
        result.Should().SucceedWith(true);
    }
    
    [Fact]
    public async Task Minimal_from_build_dir()
    {
        var streamGenerator = StreamGenerator.Generate(stream =>
        {
            var fs = new FileSystem();
            var directoryInfo = fs.DirectoryInfo.New("TestFiles/AppDir/Minimal");
            var appDir = new DirectorioIODirectory(Maybe<string>.None, directoryInfo);
            return AppImage.FromBuildDir(stream, appDir, _ => new TestRuntime());
        });

        var result = await streamGenerator().Map(streamFactory => AreEqual(
            streamFactory,
            () => File.OpenRead("TestFiles/Results/Minimal-FromBuildDir.appimage")));
        result.Should().SucceedWith(true);
    }

    private static Task<bool> AreEqual(Func<Stream> one, Func<Stream> another)
    {
        return new AppImageStreamComparer().AreEqual(() =>
        {
            var stream = one();
            return stream.ToObservable();
        }, () => another().ToObservable());
    }
}