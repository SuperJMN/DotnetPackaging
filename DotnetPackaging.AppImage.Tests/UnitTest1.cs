using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.AppImage.Tests;

public class UnitTest1
{
    [Fact]
    public async Task SquashFS()
    {
        var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
        var root = fs.GetDirectory("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir");

        await AppImage.SquashFS.Build(root)
            .Tap(async stream =>
            {
                await using (stream)
                {
                    await stream.ToFile(fs.GetFile("c:/users/jmn/Desktop/Test.squashfs"));
                }
            });
    }
}