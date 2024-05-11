using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Builder;

public class TarEntryBuilder
{
    public static IEnumerable<TarEntry> From(UnixRoot unixRoot)
    {
        return unixRoot.Nodes.SelectMany(x => From(x, ""));
    }

    private static IEnumerable<TarEntry> From(INode node, string currentPath)
    {
        return node switch
        {
            UnixDir unixDir => From(unixDir, currentPath),
            UnixFile unixFile => From(unixFile, currentPath),
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
    }

    private static IEnumerable<TarEntry> From(UnixFile unixFile, string currentPath)
    {
        return new[]
        {
            new FileTarEntry(currentPath + "/" + unixFile.Name, unixFile.Data, TarFileProperties.From(unixFile.Properties))   
        };
    }

    private static IEnumerable<TarEntry> From(UnixDir unixDir, string rootPath)
    {
        var currentPath = rootPath + "/" + unixDir.Name;
        return new[]
        {
            new DirectoryTarEntry(currentPath, TarDirectoryProperties.From(unixDir.Properties))
        }.Concat(unixDir.Children.SelectMany(node => From(node, currentPath)));
    }
}