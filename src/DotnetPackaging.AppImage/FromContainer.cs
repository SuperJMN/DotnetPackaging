using CSharpFunctionalExtensions;
using System.Reactive.Linq;
using Zafiro.FileSystem;
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
        var execResult = root.Files().TryFirst(x => x.Name == setup.ExecutableName).ToResult($"Could not find executable file '{setup.ExecutableName}'");

        var build = execResult
            .Bind(exec => GetArch(exec)
                .Bind(architecture => runtimeFactory.Create(architecture))
                .Map(rt => new AppImage(rt, CreateRoot(root, exec))));

        return build;
    }

    private async Task<Result<Architecture>> GetArch(IData exec)
    {
        if (setup.DetectArchitecture)
        {
            return await LinuxElfInspector.GetArchitecture(exec.Bytes);
        }

        if (setup.Architecture.Equals(Architecture.All))
        {
            return Result.Failure<Architecture>("The 'All' architecture is not valid for AppImages since they require an specific AppImage Runtime.");    
        }

        if (setup.Architecture.HasNoValue)
        {
            return Result.Failure<Architecture>("Could not detect architecture");    
        }

        return setup.Architecture.Value;
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