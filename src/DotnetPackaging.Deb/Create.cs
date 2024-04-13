using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives;
using DotnetPackaging.Deb.Client;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.Deb;

public static class Create
{
    /// <summary>
    /// Create a .deb file
    /// </summary>
    /// <param name="packageDefinition">Package definition.</param>
    /// <param name="contentsPath">Folder where the application has been published. It should contain the files compiled for Linux</param>
    /// <param name="outputPathForDebFile">Path of the output .deb file.</param>
    /// <returns>A Result to indicate whether the operation succeeded or not</returns>
    public static Task<Result> Deb(PackageDefinition packageDefinition, string contentsPath, string outputPathForDebFile)
    {
        var fs = new FileSystemRoot(new ObservableFileSystem(LocalFileSystem.Create()));

        var contentDirectory = fs.GetDirectory(contentsPath.ToZafiroPath());
        var output = fs.GetFile(outputPathForDebFile.ToZafiroPath());
        return new DebBuilder().Write(contentDirectory, packageDefinition, output);
    }
}