using Zafiro.FileSystem.Lightweight;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class FromContainer
{
    private readonly ISlimDirectory root;
    private readonly ContainerOptionsSetup setup;

    public FromContainer(ISlimDirectory root, ContainerOptionsSetup setup)
    {
        this.root = root;
        this.setup = setup;
    }

    public AppImage Build()
    {
        return new AppImage(new FakeRuntime(), CreateRoot(root));
    }

    private UnixRoot CreateRoot(ISlimDirectory directory)
    {
        return new UnixRoot(new [] { new UnixDir("usr", directory.Children.Select(Create)) });
    }

    private UnixNode Create(INode node)
    {
        return node switch
        {
            IFile f => Create(f),
            ISlimDirectory d => Create(d),
            _ => throw new ArgumentOutOfRangeException(nameof(node), node, null)
        };
    }
    
    private UnixNode Create(ISlimDirectory directory)
    {
        return new UnixDir(directory.Name, directory.Children.Select(node =>
        {
            return node switch
            {
                IFile f => Create(f),
                ISlimDirectory d => Create(d),
                _ => throw new ArgumentOutOfRangeException(nameof(node), node, null)
            };
        }));
    }

    private UnixNode Create(IFile file)
    {
        return new UnixFile(file.Name, file);
    }
}