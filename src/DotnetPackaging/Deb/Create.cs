using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.Deb;

public static class Create
{
    /// <summary>
    /// Create a .deb file
    /// </summary>
    /// <param name="contentsPath">Folder where the application has been published. It should contain the files compiled for Linux</param>
    /// <param name="outputPathForDebFile">Path of the output .deb file.</param>
    /// <param name="metadata">Metadata of the .deb file.</param>
    /// <param name="executableFiles">Mapping to identify which files are executable and their properties (Desktop entries and so).</param>
    /// <returns>A Result to indicate whether the operation succeeded or not</returns>
    public static async Task<Result> Deb(string contentsPath, string outputPathForDebFile, Metadata metadata, Dictionary<ZafiroPath,ExecutableMetadata> executableFiles)
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        
        return await 
            from contentDirectory in fs.GetDirectory(contentsPath.ToZafiroPath())
            from output in fs.GetFile(outputPathForDebFile.ToZafiroPath())
            select new DebBuilder().Create(contentDirectory, metadata, executableFiles, output);
    }
}