using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Deb;

using System;

public class DebBuilder
{
    public async Task<Result> Create(IZafiroDirectory contentDirectory, Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> dict, IZafiroFile debFile)
    {
        var result = await GetContents(contentDirectory, dict).Map<IEnumerable<Content>, DebFile>(contents => new DebFile(metadata, new Contents(contents)));
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