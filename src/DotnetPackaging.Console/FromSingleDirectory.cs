using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Core;
using Serilog;
using Zafiro.FileSystem.Lightweight;
using IDirectory = Zafiro.FileSystem.Lightweight.IDirectory;

namespace DotnetPackaging.Console;

public class FromSingleDirectory
{
    private readonly IFileSystem fileSystem;

    public FromSingleDirectory(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public async Task<Result> Create(string contentsDirPath, string outputFilePath, Options singleDir)
    {
        Log.Information("Creating AppImage from Single Directory {AppDirPath} and writing to {OutputPath}...", contentsDirPath, outputFilePath);
        var directoryInfo = fileSystem.DirectoryInfo.New(contentsDirPath);
        var buildDir = new DirectorioIODirectory("", directoryInfo);
        var outputStreamResult = Result.Try(() => fileSystem.File.Open(outputFilePath, FileMode.Create));
        var result = await outputStreamResult.Bind(stream => Build(stream, singleDir, buildDir));

        return result;
    }

    private static async Task<Result> Build(Stream stream, Options singleDir, IDirectory inputDir)
    {
        using (stream)
        {
            return await AppImage.AppImage.WriteFromBuildDirectory(stream, inputDir, singleDir);
        }
    }
}