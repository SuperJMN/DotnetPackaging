using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class FromRootedTests
{
    [Fact]
    public void Paths_without_leaps()
    {
        var files = new[]
        {
            new RootedFile(ZafiroPath.Empty, new UnixFile("Sample1.txt", (StringData) "Content")),
            new RootedFile(ZafiroPath.Empty, new UnixFile("Sample2.txt", (StringData) "Content")),
            new RootedFile("Dir", new UnixFile("Sample3.txt", (StringData) "Content")),
            new RootedFile("Dir", new UnixFile("Sample4.txt", (StringData) "Content")),
            new RootedFile("Dir/Subdir", new UnixFile("Sample5.txt", (StringData) "Content")),
        };

        var root = files.ToRoot(ZafiroPath.Empty, (name, children) => new UnixDir(name, children.Cast<UnixNode>()));

        var rutas = TreeHelper.GeneratePaths<UnixNode>(root, x => x is UnixDir d ? d.Nodes : new List<UnixNode>(), x => x.Name)
            .Select(x => x.path);

        rutas.Should().BeEquivalentTo
        (
            "",
            "Sample1.txt",
            "Sample2.txt",
            "Dir",
            "Dir/Sample3.txt",
            "Dir/Sample4.txt",
            "Dir/Subdir",
            "Dir/Subdir/Sample5.txt"
        );
    }
    
    
    [Fact]
    public void Path_with_leap()
    {
        var files = new[]
        {
            new RootedFile("Dir/Subdir", new UnixFile("Sample.txt", (StringData) "Content")),
        };

        var root = files.ToRoot(ZafiroPath.Empty, (s, nodes) => new UnixDir(s, nodes.Cast<UnixNode>()));

        var rutas = TreeHelper.GeneratePaths<UnixNode>(root, x => x is UnixDir d ? d.Nodes : new List<UnixNode>(), x => x.Name)
            .Select(x => x.path);

        rutas.Should().BeEquivalentTo
        (
            "",
            "Dir",
            "Dir/Subdir",
            "Dir/Subdir/Sample.txt"
        );
    }
}

public delegate IEnumerable<TNode> ChildSelector<TNode>(TNode node);
public delegate string NameSelector<TNode>(TNode node);

public static class TreeHelper
{
    public static List<(TNode node, string path)> GeneratePaths<TNode>(TNode root, ChildSelector<TNode> getChildren, NameSelector<TNode> getName)
    {
        var result = new List<(TNode node, string path)>();
        TraverseTree(root, getChildren, getName, "", result);
        return result;
    }

    private static void TraverseTree<TNode>(TNode node, ChildSelector<TNode> getChildren, NameSelector<TNode> getName, string currentPath, List<(TNode node, string path)> result)
    {
        var nodeName = getName(node);
        var newPath = string.IsNullOrEmpty(currentPath) ? nodeName : $"{currentPath}/{nodeName}";
        
        result.Add((node, newPath));

        foreach (var child in getChildren(node))
        {
            TraverseTree(child, getChildren, getName, newPath, result);
        }
    }
}