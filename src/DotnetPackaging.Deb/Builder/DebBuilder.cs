using System.IO;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using IOPath = System.IO.Path;

namespace DotnetPackaging.Deb.Builder;

public class DebBuilder
{
    public FromContainerOptions Container(IContainer root, string? name = null)
    {
        return new FromContainerOptions(root, Maybe<string>.From(name));
    }

    public FromContainerOptions Directory(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(rootPath));
        }

        var containerResult = BuildContainer(rootPath);
        if (containerResult.IsFailure)
        {
            throw new InvalidOperationException($"Unable to convert directory '{rootPath}' into container: {containerResult.Error}");
        }

        var directoryName = IOPath.GetFileName(IOPath.GetFullPath(rootPath).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar));
        return new FromContainerOptions(containerResult.Value, Maybe<string>.From(directoryName));
    }

    private static Result<RootContainer> BuildContainer(string root)
    {
        try
        {
            var files = System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    file => NormalizeRelativePath(root, file),
                    file => (IByteSource)ByteSource.FromStreamFactory(() => File.OpenRead(file)),
                    StringComparer.Ordinal);

            return files.ToRootContainer();
        }
        catch (Exception ex)
        {
            return Result.Failure<RootContainer>(ex.Message);
        }
    }

    private static string NormalizeRelativePath(string root, string file)
    {
        var relativePath = IOPath.GetRelativePath(root, file);
        return relativePath.Replace('\\', '/');
    }
}
