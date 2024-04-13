using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Deb.Contents;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Client;

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