using DotnetPackaging.AppImage.Core;
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
                    Root = await CreateRoot(root, conf.Architecture, conf.Executable),
                    conf.Runtime,
                })
                .Map(conf => new Core.AppImage(conf.Runtime, conf.Root)));

        return build;
    }

    private async Task<UnixRoot> CreateRoot(IDirectory directory, Architecture architecture, IFile executable)
    {
        var packageMetadata = await BuildUtils.CreateMetadata(setup, directory, architecture, executable);

        var localExecPath = "$APPDIR" + "/usr/bin/" + executable.Name;
        
        var binFiles = directory.FilesInTree(ZafiroPath.Empty).Select(file => new RootedFile("usr/bin", new UnixFile(file, file.Name == executable.Name ? UnixFileProperties.ExecutableFileProperties() : UnixFileProperties.RegularFileProperties())));
        var iconFiles = packageMetadata.Icon.Match(icon => new[]
        {
            new RootedFile(ZafiroPath.Empty, new UnixFile(".AppDir", icon)),
            new RootedFile($"usr/share/icons/hicolor/{icon.Size}x{icon.Size}/apps", new UnixFile(packageMetadata.Package.ToLower() + ".png", icon))
        }, Enumerable.Empty<RootedFile>);

        IEnumerable<IRootedFile> files = new[]
            {
                new RootedFile("usr/bin", new UnixFile(packageMetadata.Package.ToLower(), (StringData) TextTemplates.RunScript(localExecPath), UnixFileProperties.ExecutableFileProperties())),
                new RootedFile(ZafiroPath.Empty, new UnixFile("AppRun", (StringData) TextTemplates.RunScript(localExecPath), UnixFileProperties.ExecutableFileProperties())),
                new RootedFile(ZafiroPath.Empty, new UnixFile("application.desktop", new StringData(TextTemplates.DesktopFileContents(localExecPath, packageMetadata)))),
                new RootedFile("usr/share/metainfo", new UnixFile(packageMetadata.Package.ToLower() + ".appdata.xml", new StringData(TextTemplates.AppStream(packageMetadata)))),
            }
            .Concat(iconFiles)
            .Concat(binFiles);

        var dir = files.ToList().ToRoot(ZafiroPath.Empty, (name, children) => new UnixDir(name, children.Cast<UnixNode>()));
        return new UnixRoot(dir.Nodes);
    }
}