using System.IO.Abstractions;
using System.Runtime.InteropServices;
using FluentAssertions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    //[Fact]
    //public async Task CreateAppImage()
    //{
    //    var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
    //    var root = fs.GetDirectory("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir");
    //    var output = fs.GetFile("c:/users/jmn/Desktop/Avalonia.AppImage");
    //    var result =await AppImagePackager.Build(output, Architecture.X64, root);
    //    result.Should().Succeed();
    //}
}