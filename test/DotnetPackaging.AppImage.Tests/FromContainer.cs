using CSharpFunctionalExtensions;
using System.IO;
using System.Reactive.Linq;
using Zafiro.FileSystem.Lightweight;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class FromContainer
{
    private readonly ISlimDirectory root;
    private readonly RuntimeFactory runtimeFactory;
    private readonly ContainerOptionsSetup setup;

    public FromContainer(ISlimDirectory root, RuntimeFactory runtimeFactory, ContainerOptionsSetup setup)
    {
        this.root = root;
        this.runtimeFactory = runtimeFactory;
        this.setup = setup;
    }

    public Task<Result<AppImage>> Build()
    {
        var executable = root.Files().TryFirst(x => x.Name == setup.ExecutableName).ToResult($"Could not find executable file '{setup.ExecutableName}'");

        var build = executable
            .Bind(async exec =>
            {
                if (setup.DetectArchitecture)
                {
                    var archResult = await LinuxElfInspector.GetArchitecture(exec.Bytes);
                    return archResult
                        .Map(arch => new AppImage(runtimeFactory.Create(arch), CreateRoot(root, exec)));
                }

                return setup.Architecture
                    .Map(ar => new AppImage(runtimeFactory.Create(ar), CreateRoot(root, exec)))
                    .ToResult("The architecture has not be specified");
            });
        
        return build;
    }

    private UnixRoot CreateRoot(ISlimDirectory directory, IFile executable)
    {
        return new UnixRoot(new UnixNode[]
        {
            new UnixDir("usr", directory.Children.Select(Create)),
            new UnixDir("bin", new UnixNode [] { new UnixFile(executable, UnixFileProperties.ExecutableFileProperties()) }),
            new UnixFile("AppRun", new StringData(TextTemplates.RunScript("$APPDIR" + "/" + directory.Name + "/" + executable.Name))),
        });
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