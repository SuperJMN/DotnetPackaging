﻿using DotnetPackaging.Archives.Deb;
using FluentAssertions;
using SixLabors.ImageSharp;
using Zafiro.FileSystem;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public class DebCreationTests
{
    [Fact]
    public async Task Local_deb_builder()
    {
        var result  = await Create.Deb(
            contentsPath: @"TestFiles\Content", 
            outputPathForDebFile: @"C:\Users\JMN\Desktop\Testing\SampleOther.deb", 
            metadata: TestData.Metadata,
            executableFiles: TestData.ExecutableFiles);

        result.Should().Succeed();
    }

}

public static class TestData
{
    public static DesktopEntry DesktopEntry => new()
    {
        Name = "Avalonia Syncer",
        Icons = IconResources.Create(new IconData(32, Image.Load("TestFiles\\icon.png"))).Value,
        StartupWmClass = "AvaloniaSyncer",
        Keywords = new[] { "file manager" },
        Comment = "The best file explorer ever",
        Categories = new [] { "FileManager", "Filesystem", "Utility", "FileTransfer", "Archiving"}
    };

    public static Metadata Metadata => new()
    {
        PackageName = "AvaloniaSyncer",
        Description = "Best file explorer you'll ever find",
        ApplicationName = "Avalonia Syncer",
        Architecture = "amd64",
        Homepage = "https://www.something.com",
        License = "MIT",
        Maintainer = "SuperJMN@outlook.com",
        Version = "0.1.33"
    };

    public static Dictionary<ZafiroPath, ExecutableMetadata> ExecutableFiles => new()
    {
        ["TestDebpackage.Desktop"] = new("avaloniasyncer", DesktopEntry),
    };
}