using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using DotnetPackaging.New.Archives.Deb;
using DotnetPackaging.New.Archives.Deb.Contents;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Build;

public class DebBuilder
{
    public async Task<Result> Write(IZafiroDirectory contentDirectory, Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> dict, IZafiroFile debFile)
    {
        var result = await ContentCollection.From(contentDirectory, dict)
            .Map(contents => new DebFile(metadata, new ContentCollection(contents)))
            .Tap(deb => deb.Bytes.DumpTo(debFile));

        return result;
    }
}