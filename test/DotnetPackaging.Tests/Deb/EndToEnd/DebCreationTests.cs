using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb;
using DotnetPackaging.Tests.Tar;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public class DebCreationTests
{
    [Fact]
    public async Task Create()
    {
        var metadata = new Metadata
        {
            PackageName = "AvaloniaSyncer",
            Description = "Best file explorer you'll ever find",
            ApplicationName = "Avalonia Syncer",
            Architecture = "amd64",
            Homepage = "https://www.something.com",
            License = "MIT",
            Maintainer = "SuperJMN@outlook.com"
        };

        var desktopEntry = new DesktopEntry()
        {
            Name = "Avalonia Syncer",
            Icons = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable()))).Value,
            StartupWmClass = "AvaloniaSyncer",
            Keywords = new[] { "file manager" },
            Comment = "The best file explorer ever",
            Categories = new [] { "FileManager", "Filesystem", "Utility", "FileTransfer", "Archiving"}
        };

        var dict = new Dictionary<ZafiroPath, ExecutableMetadata>()
        {
            ["AvaloniaSyncer.Desktop"] = new("avaloniasyncer", desktopEntry),
        };

        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        var result = await fs
            .GetDirectory("C:/Users/JMN/Desktop/Testing/AvaloniaSyncer")
            .Bind(directory => GetContents(directory, dict).Map(contents => new DebFile(metadata, new Contents(contents))));

        await result.Tap(deb => deb.Bytes.DumpTo("C:\\Users\\JMN\\Desktop\\Testing\\AvaloniaSyncer.deb"));

        result.Should().Succeed();
    }

    private Task<Result<IEnumerable<Content>>> GetContents(IZafiroDirectory directory, Dictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        return directory.GetFilesInTree().Map(files => files.Select(file => GetContent(directory, file, desktopEntries)));
    }

    private Content GetContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, IReadOnlyDictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        return desktopEntries
            .TryFind(file.Path.Name())
            .Match(
                entry => ExecutableContent(zafiroDirectory, file, entry), 
                () => RegularContent(zafiroDirectory, file));
    }

    private RegularContent RegularContent(IZafiroDirectory zafiroDirectory, IZafiroFile file)
    {
        return new RegularContent(file.Path.MakeRelativeTo(zafiroDirectory.Path), () =>  GetFileContents(file));
    }

    private Content ExecutableContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, ExecutableMetadata metadata)
    {
        return new ExecutableContent(file.Path.MakeRelativeTo(zafiroDirectory.Path), () => GetFileContents(file))
        {
            DesktopEntry = metadata.DesktopEntry,
            CommandName = metadata.CommandName,
        };
    }

    private IObservable<byte> GetFileContents(IZafiroFile file)
    {
        return Zafiro.Mixins.ObservableEx.Using(async () => (await file.GetContents()).Value, stream => stream.ToObservable());
    }
}

public record ExecutableMetadata(string CommandName, DesktopEntry DesktopEntry);
