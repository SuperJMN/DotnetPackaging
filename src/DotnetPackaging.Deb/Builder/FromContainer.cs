using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Builder;

public class FromContainer
{
    private readonly IDirectory root;
    private readonly ContainerOptionsSetup setup;

    public FromContainer(IDirectory root, ContainerOptionsSetup setup)
    {
        this.root = root;
        this.setup = setup;
    }

    public Task<Result<DebFile>> Build()
    {
        var execResult = GetExecutable();

        var build = execResult
            .Bind(exec => GetArch(exec).Tap(arch => Log.Information("Architecture set to {Arch}", arch))
                .Map(async architecture => new
                {
                    PackageMetadata = await GetPackageMetadata(root, architecture),
                    Executable = exec
                }))
            .Map(conf => new DebFile(conf.PackageMetadata, TarEntryBuilder.From(root, conf.PackageMetadata, conf.Executable).ToArray()));

        return build;
    }

    private async Task<PackageMetadata> GetPackageMetadata(IDirectory directory, Architecture architecture)
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
            VcsBrowser = setup.VcsBrowser,
            VcsGit = setup.VcsGit,
            InstalledSize = setup.InstalledSize,
            ModificationTime = setup.ModificationTime,
        };

        return packageMetadata;
    }

    private Task<Result<IFile>> GetExecutable()
    {
        return setup.ExecutableName.Match(ExecutableLookupByName, ExecutableLookupWithoutName);
    }

    private async Task<Result<IFile>> ExecutableLookupByName(string execName)
    {
        var result = await Task.FromResult(root.Files().TryFirst(x => x.Name == execName).ToResult($"Could not find executable file '{setup.ExecutableName}'"));
        Log.Information("Looking up for executable named '{ExecName}'", execName);
        return result.Tap(() => Log.Information("Executable found successfully"));
    }

    private async Task<Result<IFile>> ExecutableLookupWithoutName()
    {
        Log.Information("No executable has been specified. Looking up for candidates.");
        var execFiles = await root.Files()
            .ToObservable()
            .Select(async file => (await file.IsElf()).Map(isElf => new{ IsElf = isElf, File = file }))
            .Concat()
            .Successes()
            .Where(x => x.IsElf && !x.File.Name.EndsWith(".so"))
            .Select(x => x.File)
            .ToList();
        return execFiles
            .TryFirst()
            .ToResult("No executable has been specified Could not find any executable")
            .Tap(file => Log.Information("Choosing {Executable}", file));
    }

    private Task<Result<Architecture>> GetArch(IFile exec)
    {
        return setup.Architecture
            .Tap(architecture => Log.Information("Architecture set to {Arch}", architecture))
            .Map(x =>
            {
                if (x == Architecture.All)
                {
                    return Result.Failure<Architecture>("The 'All' architecture is not valid for AppImages since they require an specific AppImage Runtime");
                }

                return Result.Success(x);
            })
            .Or(async () => await exec.GetArchitecture())
            .ToResult("Could not determine the architecture")
            .Bind(result => result);
    }

    private async Task<Result<IIcon>> GetIcon(IDirectory directory)
    {
        string[] icons = ["App.png", "Application.png", "AppImage.png", "Icon.png"];
        if (setup.Icon.HasNoValue)
        {
            var maybeFile = directory.Files()
                .TryFirst(x => icons.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
                .ToResult($"Icon autodetection: Could not find any icon in '{directory}'. We've looked for: {string.Join(",", icons.Select(x => $"\"{x}\""))}")
                .Tap(f => Log.Information("Found icon in file {File}", f));

            return await maybeFile.Map(Icon.FromData);
        }

        return setup.Icon.ToResult("No icon has been specified");
    }
}