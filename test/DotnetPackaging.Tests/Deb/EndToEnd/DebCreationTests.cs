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
        var metadata = new Metadata()
        {
            PackageName = "AvaloniaSyncer",
            Description = "Best file explorer you'll ever find",
            ApplicationName = "Avalonia Syncer",
            Architecture = "amd64",
            Homepage = "https://www.something.com",
            License = "MIT",
            Maintainer = "Me"
        };

        var desktopEntry = new DesktopEntry()
        {
            Name = "Avalonia Syncer",
            Icons = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable()))).Value,
            StartupWmClass = "Avalonia Syncer",
            Keywords = new[] { "file manager" },
        };

        var dict = new Dictionary<ZafiroPath, DesktopEntry>()
        {
            ["AvaloniaSyncer.Desktop"] = desktopEntry,
        };

        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        var result = await fs
            .GetDirectory("C:/Users/JMN/Desktop/Testing/AvaloniaSyncer")
            .Bind(directory => GetContents(directory, dict).Map(contents => new DebFile(metadata, new Contents(contents))));

        await result.Tap(deb => deb.Bytes.DumpTo("C:\\Users\\JMN\\Desktop\\Testing\\AvaloniaSyncer.deb"));

        result.Should().Succeed();
    }

    private Task<Result<IEnumerable<Content>>> GetContents(IZafiroDirectory directory, Dictionary<ZafiroPath, DesktopEntry> desktopEntries)
    {
        return directory.GetFilesInTree().Map(files => files.Select(file => GetContent(file, desktopEntries)));
    }

    private Content GetContent(IZafiroFile file, IReadOnlyDictionary<ZafiroPath, DesktopEntry> desktopEntries)
    {
        return desktopEntries
            .TryFind(file.Path.Name())
            .Match(
                entry => ExecutableContent(file, entry), 
                () => RegularContent(file));
    }

    private RegularContent RegularContent(IZafiroFile file)
    {
        return new RegularContent(file.Path, () =>  GetFileContents(file));
    }

    private Content ExecutableContent(IZafiroFile file, DesktopEntry entry)
    {
        return new ExecutableContent(file.Path, () => GetFileContents(file))
        {
            DesktopEntry = entry,
        };
    }

    private IObservable<byte> GetFileContents(IZafiroFile file)
    {
        return Zafiro.Mixins.ObservableEx.Using(async () => (await file.GetContents()).Value, stream => stream.ToObservable());
    }
}
