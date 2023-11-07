using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Deb;
using FluentAssertions;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public class DebCreationTests
{
    public DesktopEntry DesktopEntry => new()
    {
        Name = "Avalonia Syncer",
        Icons = IconResources.Create(new IconData(32, new FileInfo("TestFiles\\icon.png").ToByteStore())).Value,
        StartupWmClass = "AvaloniaSyncer",
        Keywords = new[] { "file manager" },
        Comment = "The best file explorer ever",
        Categories = new [] { "FileManager", "Filesystem", "Utility", "FileTransfer", "Archiving"}
    };

    public Metadata Metadata => new()
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

    public Dictionary<ZafiroPath, ExecutableMetadata> ExecutableFiles => new()
    {
        ["AvaloniaSyncer.Desktop"] = new("avaloniasyncer", DesktopEntry),
    };

    [Fact]
    public async Task Local_deb_builder()
    {
        var result  = await Create.Deb(
            contentsPath: @"C:\Users\JMN\Desktop\Testing\AvaloniaSyncer", 
            outputPathForDebFile: @"C:\Users\JMN\Desktop\Testing\AvaloniaSyncer.deb", 
            metadata: Metadata,
            executableFiles: ExecutableFiles);

        result.Should().Succeed();
    }
}


