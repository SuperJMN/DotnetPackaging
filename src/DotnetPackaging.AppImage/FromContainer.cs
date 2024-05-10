using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;
using DotnetPackaging.AppImage.Tests;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage;

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
                .Map(async runtime => new { Root = await CreateRoot(root, exec), Runtime = runtime })
                .Map(conf => new AppImage(conf.Runtime, conf.Root)));

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

    private async Task<UnixRoot> CreateRoot(ISlimDirectory directory, IFile executable)
    {
        var mandatory = new UnixNode[]
        {
            new UnixDir("usr", new List<UnixNode>()
            {
                new UnixDir("bin", directory.Children.Select(Create)),
            }),
            new UnixFile("AppRun", new StringData(TextTemplates.RunScript("$APPDIR" + "/usr/bin/" + executable.Name)), UnixFileProperties.ExecutableFileProperties()),
        };

        var optionalNodes = (await GetIcon(directory).Map(icon1 => new UnixFile(".AppDir", icon1)).TapError(Log.Warning)).AsMaybe().ToList();
        var nodes = mandatory.Concat(optionalNodes);
        
        return new UnixRoot(nodes);
    }

    private async Task<Result<IIcon>> GetIcon(ISlimDirectory directory)
    {
        string[] icons = ["App.png", "Application.png", "AppImage.png", "Icon.png" ];
        if (setup.DetectIcon)
        {
            var maybeFile = directory.Files().TryFirst(x => icons.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToResult("Not found");
            return await maybeFile.Map(Icon.FromData);
        }

        return setup.Icon.ToResult("No icon has been specified");
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
        var permissions = setup.ExecutableName.Equals(file.Name) ? UnixFileProperties.ExecutableFileProperties() : UnixFileProperties.RegularFileProperties();
        return new UnixFile(file.Name, file, permissions);
    }
}