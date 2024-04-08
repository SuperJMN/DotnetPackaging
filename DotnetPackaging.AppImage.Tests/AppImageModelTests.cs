using DotnetPackaging.AppImage.Model;
using FluentAssertions;
using Zafiro.FileSystem.Lightweight;

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

        var result = await new AppImageBuilder()
            .WithDesktopMetadata(desktopMetadata)
            .Build(new BlobContainer(new List<IBlob>(), new List<IBlobContainer>()), new TestRuntime(), new DefaultScriptAppRun("/usr/bin/Application/App.Desktop"));

        result.Should().Succeed();
    }
}