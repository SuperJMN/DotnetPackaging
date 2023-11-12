using CSharpFunctionalExtensions;
using DotnetPackaging.Archives.Deb;
using DotnetPackaging.Archives.Deb.Contents;
using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Build;

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