using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public class DebCreationTests
{
    [Fact]
    public async Task Create_deb_file_AvaloniaSyncer()
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);

        var desktopEntry = new DesktopEntry()
        {
            Name = "Avalonia Syncer",
            Icons = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable()))).Value,
            StartupWmClass = "AvaloniaSyncer",
            Keywords = new[] { "file manager" },
            Comment = "The best file explorer ever",
            Categories = new [] { "FileManager", "Filesystem", "Utility", "FileTransfer", "Archiving"}
        };

        var metadata = new Metadata
        {
            PackageName = "AvaloniaSyncer",
            Description = "Best file explorer you'll ever find",
            ApplicationName = "Avalonia Syncer",
            Architecture = "amd64",
            Homepage = "https://www.something.com",
            License = "MIT",
            Maintainer = "SuperJMN@outlook.com",
            Version = "v0.1.33"
        };

        var executableMetadatas = new Dictionary<ZafiroPath, ExecutableMetadata>()
        {
            ["AvaloniaSyncer.Desktop"] = new("avaloniasyncer", desktopEntry),
        };

        var creation =
            from contentDirectory in fs.GetDirectory("C:/Users/JMN/Desktop/Testing/AvaloniaSyncer")
            from output in fs.GetFile("C:/Users/JMN/Desktop/Testing/AvaloniaSyncer.deb")
            select new DebBuilder().Create(contentDirectory, metadata, executableMetadatas, output);

        var result = await creation;
        result.Should<Task<Result>>().Succeed();
    }
}