using CSharpFunctionalExtensions;
using DotnetPackaging.Archives.Deb;
using DotnetPackaging.Archives.Deb.Contents;
using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Client;

public class DebBuilder
{
    public async Task<Result> Write(IZafiroDirectory contentDirectory, PackageDefinition packageDefinition, IZafiroFile debFile)
    {
        var result = await ContentCollection.From(contentDirectory, packageDefinition.ExecutableMappings)
            .Map(contents => new DebFile(packageDefinition.Metadata, new ContentCollection(contents)))
            .Bind(deb => deb.Bytes.DumpTo(debFile));

        return result;
    }
}