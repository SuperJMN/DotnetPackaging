using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Builder;

public class BinDirectory
{
    private readonly IEnumerable<INode> directoryChildren;
    private readonly IFile executable;

    private BinDirectory(IEnumerable<INode> directoryChildren, IFile executable)
    {
        this.directoryChildren = directoryChildren;
        this.executable = executable;
    }

    private UnixNode Create(INode node)
    {
        return node switch
        {
            IFile f => Create(f),
            IDirectory d => Create(d),
            _ => throw new ArgumentOutOfRangeException(nameof(node), node, null)
        };
    }

    private UnixNode Create(IDirectory directory)
    {
        return new UnixDir(directory.Name, directoryChildren.Select(node =>
        {
            return node switch
            {
                IFile f => Create(f),
                IDirectory d => Create(d),
                _ => throw new ArgumentOutOfRangeException(nameof(node), node, null)
            };
        }));
    }

    private UnixNode Create(IFile file)
    {
        var permissions = executable.Name.Equals(file.Name) ? UnixFileProperties.ExecutableFileProperties() : UnixFileProperties.RegularFileProperties();
        return new UnixFile(file.Name, file, permissions);
    }

    public static IEnumerable<UnixNode> Create(IEnumerable<INode> directoryChildren, IFile file)
    {
        var binCreator = new BinDirectory(directoryChildren, file);
        return binCreator.Create();
    }

    private IEnumerable<UnixNode> Create()
    {
        return directoryChildren.Select(Create);
    }
}