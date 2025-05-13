using DotnetPackaging.AppImage.Core;
using Serilog;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Readonly;
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

    public Task<Result<Core.AppImage>> Build()
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
                    Root = await CreateRoot(root, conf.Architecture, conf.Executable, setup.IsTerminal),
                    conf.Runtime,
                })
                .Map(conf => new Core.AppImage(conf.Runtime, conf.Root)));

        return build;
    }

    private async Task<UnixRoot> CreateRoot(IDirectory directory, Architecture architecture, IFile executable, bool isTerminal)
    {
        var packageMetadata = await BuildUtils.CreateMetadata(setup, directory, architecture, executable, isTerminal);

        var localExecPath = "$APPDIR" + "/usr/bin/" + executable.Name;

        // Those should go to usr/bin
        var directoryFiles = directory.RootedFiles().Select(file =>
        {
            var newPath = ((ZafiroPath)"usr/bin").Combine(file.Path);
            return new RootedFile(newPath, new UnixFile(file, file.Name == executable.Name ? UnixFileProperties.ExecutableFileProperties() : UnixFileProperties.RegularFileProperties()));
        });
        
        var iconFiles = packageMetadata.Icon.Match(icon => new[]
        {
            new RootedFile(ZafiroPath.Empty, new UnixFile(".DirIcon", icon)),
            new RootedFile(ZafiroPath.Empty, new UnixFile(packageMetadata.Package + ".png", icon)),
            new RootedFile($"usr/share/icons/hicolor/{icon.Size}x{icon.Size}/apps", new UnixFile(packageMetadata.Package.ToLower() + ".png", icon))
        }, Enumerable.Empty<RootedFile>);


        IEnumerable<IRootedFile> files = new[]
            {
                new RootedFile("usr/bin", new UnixFile(packageMetadata.Package.ToLower(), Data.FromString(localExecPath), UnixFileProperties.ExecutableFileProperties())),
                new RootedFile(ZafiroPath.Empty, new UnixFile("AppRun", Data.FromString(TextTemplates.RunScript(localExecPath)), UnixFileProperties.ExecutableFileProperties())),
                new RootedFile(ZafiroPath.Empty, new UnixFile("application.desktop", Data.FromString(TextTemplates.DesktopFileContents(localExecPath, packageMetadata)))),
                new RootedFile("usr/share/metainfo", new UnixFile(packageMetadata.Package.ToLower() + ".appdata.xml", Data.FromString(TextTemplates.AppStream(packageMetadata)))),
            }
            .Concat(iconFiles)
            .Concat(directoryFiles);

        var dir = files.ToList().ToRoot(ZafiroPath.Empty, (name, children) => new UnixDir(name, children.Cast<UnixNode>()));
        return new UnixRoot(dir.Nodes);
    }
}