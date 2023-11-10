using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using DotnetPackaging.New.Archives.Deb;
using DotnetPackaging.New.Archives.Deb.Contents;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Build;

public class DebBuilder
{
    public async Task<Result> Create(IZafiroDirectory contentDirectory, Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> dict, IZafiroFile debFile)
    {
        var result = await GetContents(contentDirectory, dict).Map<IEnumerable<Content>, DebFile>(contents => new DebFile(metadata, new ContentCollection(contents)));
        await result.Tap(deb => deb.Bytes.DumpTo(debFile));
        return result;
    }

    private Task<Result<IEnumerable<Content>>> GetContents(IZafiroDirectory directory, Dictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        var contents = directory
            .GetFilesInTree()
            .Bind(async files =>
            {
                var contentRetrievingTasks = files.Select(file => GetContent(directory, file, desktopEntries));
                var results = await Task.WhenAll(contentRetrievingTasks.ToArray());
                return results.Combine();
            });

        return contents;
    }

    private Task<Result<Content>> GetContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, IReadOnlyDictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        return desktopEntries.TryFind(file.Path.Name())
            .Match(
                entry => ExecutableContent(zafiroDirectory, file, entry),
                () => RegularContent(zafiroDirectory, file));
    }

    private Task<Result<Content>> RegularContent(IZafiroDirectory zafiroDirectory, IZafiroFile file)
    {
        return file
            .ToByteFlow()
            .Map(flow => (Content)new RegularContent(file.Path.MakeRelativeTo(zafiroDirectory.Path), flow));
    }

    private Task<Result<Content>> ExecutableContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, ExecutableMetadata metadata)
    {
        return file
            .ToByteFlow()
            .Map(flow => (Content) new ExecutableContent(file.Path.MakeRelativeTo(zafiroDirectory.Path), flow)
            {
                DesktopEntry = metadata.DesktopEntry,
                CommandName = metadata.CommandName,
            });
    }
}