using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.FileSystem.Lightweight;
using IDirectory = Zafiro.FileSystem.Lightweight.IDirectory;

namespace DotnetPackaging.Console;

public class FromAppDir
{
    private readonly IFileSystem fileSystem;

    public FromAppDir(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public async Task<Result> Create(string appDirPath, string outputFilePath, Architecture architecture)
    {
        Log.Information("Creating AppImage from AppDir {AppDirPath} and writing to {OutputPath}...", appDirPath, outputFilePath);
        var directoryInfo = fileSystem.DirectoryInfo.New(appDirPath);
        var buildDir = new DirectorioIODirectory("", directoryInfo);
        var outputStreamResult = Result.Try(() => fileSystem.File.Open(outputFilePath, FileMode.Create));
        var result = await outputStreamResult.Bind(stream => Build(stream, buildDir, architecture));

        return result;
    }

    private static async Task<Result> Build(Stream stream, IDirectory inputDir, Architecture architecture)
    {
        using (stream)
        {
            return await AppImage.AppImage.WriteFromAppDir(stream, inputDir, architecture);
        }
    }
}