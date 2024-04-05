using System.IO.Abstractions.TestingHelpers;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using FluentAssertions;
using Zafiro.FileSystem.Local;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageModelTests
{
    [Fact]
    public async Task AppImageTest()
    {
        var appdir = "c:/users/jmn/Desktop/AvaloniaSyncer.AppDir";
        
        var desktopMetadata = new DesktopMetadata()
        {
            Categories = new[] { "Category" },
            Comment = "Comment",
            Keywords = ["Keywords"],
            Name = "Application",
            StartupWmClass = "Application",
        };

        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
        {
            [$"{appdir}/SomeExe"] = new("Contents of executable file"),
            [$"{appdir}/SomeLib.dll"] = new("Contents of Some Lib"),
            [$"{appdir}/Subdir/OtherLib.dll"] = new("Contents of Other Lib"),
        });
        
        var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(mockFileSystem)));

        var root = fs.GetDirectory("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir");

        var result = await new AppImageBuilder().Build(root, new TestRuntime());
        result.Should().Succeed();
    }
}

