using Serilog;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Builder;

public class FromContainer
{
    private readonly IDirectory root;
    private readonly RuntimeFactory runtimeFactory;
    private readonly FromDirectoryOptions setup;

    public FromContainer(IDirectory root, RuntimeFactory runtimeFactory, FromDirectoryOptions setup)
    {
        this.root = root;
        this.runtimeFactory = runtimeFactory;
        this.setup = setup;
    }

    public Task<Result<Kernel.AppImage>> Build()
    {
        var build = BuildUtils.GetExecutable(root, setup)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Tap(arch => Log.Information("Architecture set to {Arch}", arch))
                .Bind(architecture => runtimeFactory.Create(architecture)
                    .Map(runtime => new
                    {
                        Runtime = runtime,
                        Architecture = architecture,
                        Executable = exec
                    }))
                .Map(async conf => new
                {
                    Root = await CreateRoot(root, conf.Architecture, conf.Executable),
                    conf.Runtime,
                })
                .Map(conf => new Kernel.AppImage(conf.Runtime, conf.Root)));

        return build;
    }

    private async Task<UnixRoot> CreateRoot(IDirectory directory, Architecture architecture, IFile executable)
    {
        var packageMetadata = await BuildUtils.CreateMetadata(setup, directory, architecture, executable);

        var localExecPath = "$APPDIR" + "/usr/bin/" + executable.Name;
        
        var files = new[]
        {
            new RootedUnixFile(ZafiroPath.Empty, new UnixFile("Sample1.txt", (StringData) "Content")),
            new RootedUnixFile(ZafiroPath.Empty, new UnixFile("Sample2.txt", (StringData) "Content")),
            new RootedUnixFile("Dir", new UnixFile("Sample3.txt", (StringData) "Content")),
            new RootedUnixFile("Dir", new UnixFile("Sample4.txt", (StringData) "Content")),
            new RootedUnixFile("Dir/Subdir", new UnixFile("Sample5.txt", (StringData) "Content")),
        };
        
        var mandatory = new UnixNode[]
        {
            new UnixDir("usr", new List<UnixNode>()
            {
                new UnixDir("bin", BinDirectory.Create(directory.Children, executable)),
            }),
            new UnixFile("AppRun", new StringData(TextTemplates.RunScript(localExecPath)), UnixFileProperties.ExecutableFileProperties()),
            new UnixFile("application.desktop", new StringData(TextTemplates.DesktopFileContents(localExecPath, packageMetadata)), UnixFileProperties.ExecutableFileProperties()),
        };

        var optionalNodes = packageMetadata.Icon.Map(data => new UnixFile(".AppDir", data)).ToList();
        var nodes = mandatory.Concat(optionalNodes);

        return new UnixRoot(nodes);
    }
}