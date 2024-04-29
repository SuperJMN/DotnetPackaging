using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public static class DebMixin
{
    public static IEnumerable<ZafiroPath> DirectoryPaths(this IEnumerable<ZafiroPath> filePaths)
    {
        return filePaths
            .Select(x => x.Parent())
            .Distinct()
            .Select(x => x.Parents().Append(x))
            .SelectMany(x => x);
    }
}