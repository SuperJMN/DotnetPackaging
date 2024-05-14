using System.Diagnostics;
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

        var bin = BinDirectory.Create(directory.Children, executable)
            .OfType<UnixFile>()
            .Select(x => new RootedUnixFile("usr/bin", new UnixFile(x, UnixFileProperties.RegularFileProperties())));

        var second = packageMetadata.Icon.Match(icon => new[]
        {
            new RootedUnixFile(ZafiroPath.Empty, new UnixFile(".AppDir", icon)),
            new RootedUnixFile($"usr/share/icons/hicolor/{icon.Size}x{icon.Size}", new UnixFile(packageMetadata.Package.ToLower() + ".png", icon))
        }, Enumerable.Empty<RootedUnixFile>);
        
        IEnumerable<RootedUnixFile> files = new[]
            {
                new RootedUnixFile(ZafiroPath.Empty, new ("AppRun", (StringData) TextTemplates.RunScript(localExecPath), UnixFileProperties.ExecutableFileProperties())),
                new RootedUnixFile(ZafiroPath.Empty, new ("application.desktop", new StringData(TextTemplates.DesktopFileContents(localExecPath, packageMetadata)), UnixFileProperties.ExecutableFileProperties())),
            }
            .Concat(bin)
            .Concat(second);

        UnixDir dir = (UnixDir) files.ToList().FromRootedFiles(ZafiroPath.Empty);

        Debugger.Launch();
        return new UnixRoot(dir.Nodes);
    }
}