using Zafiro.FileSystem;

namespace Archiver.Deb;

public class DebPaths
{
    private readonly string packageName;
    private readonly IEnumerable<ZafiroPath> relativePaths;

    public DebPaths(string packageName, IEnumerable<ZafiroPath> relativePaths)
    {
        this.packageName = packageName;
        this.relativePaths = relativePaths;
    }

    public IEnumerable<ZafiroPath> DebPackagePaths()
    {
        ZafiroPath root = $"./usr/local/bin/{packageName}";
        return relativePaths.Select(path => root.Combine(path));
    }

    public IEnumerable<ZafiroPath> Directories() => DebPackagePaths().SelectMany(x => x.Parents()).Distinct();
}