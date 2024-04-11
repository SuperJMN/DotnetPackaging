using System.IO.Abstractions;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Console;

public class FromAppDir
{
    private readonly IFileSystem fileSystem;

    public FromAppDir(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }
    
    public async Task<Result> Create(DirectoryInfo contents, FileInfo outputFile, string desktopFile)
    {
        var maybeDesktopMetadata = Maybe.From(desktopFile);
        var maybeDesktopMetadataResult = maybeDesktopMetadata.Map(s => Result.Try(() => JsonSerializer.Deserialize<DesktopMetadata>(s)));
        var directoryInfo = fileSystem.DirectoryInfo.New(contents.FullName);
        var buildDir = new DirectorioIODirectory("", directoryInfo);
        var outputStreamResult = Result.Try(() => fileSystem.File.Open(outputFile.FullName, FileMode.Create));

        var result = await maybeDesktopMetadataResult.Match(desktopResult =>
        {
            return outputStreamResult
                .Bind(stream => desktopResult.Map(desktopMetadata => (stream, metadata: desktopMetadata)))
                .Bind(tuple => AppImageFactory.FromBuildDir(buildDir, tuple.metadata, architecture => new UriRuntime(architecture)).Map(img => (img, tuple.stream)))
                .Bind(async image =>
                {
                    await using var fileSystemStream = image.stream;
                    return await AppImageWriter.Write(fileSystemStream, image.img);
                });
        }, () =>
        {
            return outputStreamResult
                .Bind(stream => AppImageFactory.FromBuildDir(buildDir, Maybe<DesktopMetadata>.None, architecture => new UriRuntime(architecture)).Map(img => (img, stream)))
                .Bind(async image =>
                {
                    await using var fileSystemStream = image.stream;
                    return await AppImageWriter.Write(fileSystemStream, image.img);
                });
        });

        return result;
    }
}