using System.Collections.ObjectModel;
using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Archives.Deb.Contents;

public class ContentCollection : Collection<Content>
{
    public ContentCollection(IEnumerable<Content> contents) : base(contents.ToList())
    {
    }

    public static Task<Result<ContentCollection>> From(IZafiroDirectory directory, Dictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        var contents = directory
            .GetFilesInTree()
            .Bind(async files =>
            {
                var contentRetrievingTasks = files.Select(file => GetContent(directory, file, desktopEntries));
                var results = await Task.WhenAll(contentRetrievingTasks.ToArray());
                return results.Combine();
            })
            .Map(c => new ContentCollection(c));

        return contents;
    }

    private static Task<Result<Content>> GetContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, IReadOnlyDictionary<ZafiroPath, ExecutableMetadata> desktopEntries)
    {
        return desktopEntries.TryFind(file.Path.Name())
            .Match(
                entry => ExecutableContent(zafiroDirectory, file, entry),
                () => RegularContent(zafiroDirectory, file));
    }

    private static Task<Result<Content>> RegularContent(IZafiroDirectory zafiroDirectory, IZafiroFile file)
    {
        return file
            .ToByteFlow()
            .Map(flow => (Content)new RegularContent(file.Path.MakeRelativeTo(zafiroDirectory.Path), flow));
    }

    private static Task<Result<Content>> ExecutableContent(IZafiroDirectory zafiroDirectory, IZafiroFile file, ExecutableMetadata metadata)
    {
        return file
            .ToByteFlow()
            .Map(flow => (Content)new ExecutableContent(file.Path.MakeRelativeTo(zafiroDirectory.Path), flow)
            {
                DesktopEntry = metadata.DesktopEntry,
                CommandName = metadata.CommandName,
            });
    }
}