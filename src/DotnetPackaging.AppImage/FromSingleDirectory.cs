using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;
using IDirectory = Zafiro.FileSystem.Lightweight.IDirectory;

namespace DotnetPackaging.AppImage;

public class FromSingleDirectory
{
    private readonly IFileSystem fileSystem;

    public FromSingleDirectory(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public async Task<Result> Create(DirectoryInfo contents, FileInfo outputFile, SingleDirMetadata singleDirMetadata)
    {
        var directoryInfo = fileSystem.DirectoryInfo.New(contents.FullName);
        var buildDir = new DirectorioIODirectory("", directoryInfo);
        var outputStreamResult = Result.Try(() => fileSystem.File.Open(outputFile.FullName, FileMode.Create));
        var result = await outputStreamResult.Bind(stream => Build(stream, singleDirMetadata, buildDir));

        return result;
    }

    private static async Task<Result> Build(Stream stream, SingleDirMetadata singleDirMetadata, IDirectory inputDir)
    {
        using (stream)
        {
            return await AppImage.WriteFromBuildDirectory(stream, inputDir, singleDirMetadata);
        }
    }
}