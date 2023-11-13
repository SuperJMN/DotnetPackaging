using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging.Client;
using DotnetPackaging.Common;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging;

public static class Create
{
    /// <summary>
    /// Create a .deb file
    /// </summary>
    /// <param name="packageDefinition">Package definition.</param>
    /// <param name="contentsPath">Folder where the application has been published. It should contain the files compiled for Linux</param>
    /// <param name="outputPathForDebFile">Path of the output .deb file.</param>
    /// <returns>A Result to indicate whether the operation succeeded or not</returns>
    public static async Task<Result> Deb(PackageDefinition packageDefinition, string contentsPath, string outputPathForDebFile)
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);

        var result = await ResultFactory.CombineAndBind(
            fs.GetDirectory(contentsPath.ToZafiroPath()),
            fs.GetFile(outputPathForDebFile.ToZafiroPath()),
            (contentDirectory, output) => new DebBuilder().Write(contentDirectory, packageDefinition, output));

        return result;
    }
}