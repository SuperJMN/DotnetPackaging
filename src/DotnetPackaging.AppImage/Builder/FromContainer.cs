using System.Reactive.Linq;
using Serilog;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Builder;

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

    public Task<Result<Kernel.AppImage>> Build()
    {
        var execResult = root.Files().TryFirst(x => x.Name == setup.ExecutableName).ToResult($"Could not find executable file '{setup.ExecutableName}'");

        var build = execResult
            .Bind(exec => GetArch(exec)
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

    private async Task<UnixRoot> CreateRoot(ISlimDirectory directory, Architecture architecture, IFile executable)
    {
        var icon = await GetIcon(directory).TapError(Log.Warning);

        var packageMetadata = new PackageMetadata()
        {
            Architecture = architecture,
            Icon = icon.AsMaybe(),
            AppId = setup.PackageId,
            AppName = setup.AppName.GetValueOrDefault(directory.Name),
            Categories = setup.Categories,
            StartupWmClass = setup.StartupWmClass,
            Comment = setup.Comment,
            Description = setup.Description,
            Homepage = setup.Homepage,
            License = setup.License,
            Priority = setup.Priority,
            ScreenshotUrls = setup.ScreenshotUrls,
            Maintainer = setup.Maintainer,
            Summary = setup.Summary,
            Keywords = setup.Keywords,
            Recommends = setup.Recommends,
            Section = setup.Section,
            Package = setup.Package.Or(setup.AppName).GetValueOrDefault(directory.Name),
            Version = setup.Version,
            ExecutableName = executable.Name,
            VcsBrowser = setup.VcsBrowser,
            VcsGit = setup.VcsGit,
            InstalledSize = setup.InstalledSize,
            ModificationTime = setup.ModificationTime,
        };

        var localExecPath = "$APPDIR" + "/usr/bin/" + executable.Name;

        var mandatory = new UnixNode[]
        {
            new UnixDir("usr", new List<UnixNode>()
            {
                new UnixDir("bin", directory.Children.Select(Create)),
            }),
            new UnixFile("AppRun", new StringData(TextTemplates.RunScript(localExecPath)), UnixFileProperties.ExecutableFileProperties()),
            new UnixFile("application.desktop", new StringData(TextTemplates.DesktopFileContents(localExecPath, packageMetadata)), UnixFileProperties.ExecutableFileProperties()),
        };

        var optionalNodes = icon.Map(data => new UnixFile(".AppDir", data)).TapError(Log.Warning).AsMaybe().ToList();
        var nodes = mandatory.Concat(optionalNodes);

        return new UnixRoot(nodes);
    }

    private async Task<Result<IIcon>> GetIcon(ISlimDirectory directory)
    {
        string[] icons = ["App.png", "Application.png", "AppImage.png", "Icon.png"];
        if (setup.DetectIcon)
        {
            var maybeFile = directory.Files()
                .TryFirst(x => icons.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
                .ToResult($"Icon autodetection: Could not find any icon in '{directory}'. We've looked for: {string.Join(",", icons.Select(x => $"\"{x}\""))}")
                .Tap(f => Log.Information("Found icon in file {File}", f));

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