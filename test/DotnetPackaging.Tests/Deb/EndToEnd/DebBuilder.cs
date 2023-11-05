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

public class DebBuilder
{
    public async Task<Result> Create(IZafiroDirectory contentDirectory, Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> dict, IZafiroFile debFile)
    {
        var result = await AsyncResultExtensionsLeftOperand.Map<IEnumerable<Content>, DebFile>(GetContents(contentDirectory, dict), contents => new DebFile(metadata, new Contents(contents)));
        await result.Tap(deb => deb.Bytes.DumpTo(debFile));
        return result;
    }

    private Task<Result<IEnumerable<Content>>> GetContents(IZafiroDirectory directory, Dictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        return directory.GetFilesInTree().Map(files => files.Select<IZafiroFile, Content>(file => GetContent(directory, file, desktopEntries)));
    }

    private Content GetContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, IReadOnlyDictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        return desktopEntries
            .TryFind(file.Path.Name())
            .Match<Content, ExecutableMetadata>(
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