using System.Diagnostics;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging;

public static class BuildUtils
{
    public static async Task<PackageMetadata> CreateMetadata(FromDirectoryOptions setup, IDirectory directory, Architecture architecture, IFile exec)
    {
        var icon = await GetIcon(setup, directory).TapError(Log.Warning);

        var packageMetadata = new PackageMetadata
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
            Package = setup.Package.Or(setup.AppName).GetValueOrDefault(exec.Name.Replace(".Desktop", "")),
            Version = setup.Version.GetValueOrDefault("1.0.0"),
            VcsBrowser = setup.VcsBrowser,
            VcsGit = setup.VcsGit,
            InstalledSize = setup.InstalledSize,
            ModificationTime = setup.ModificationTime.GetValueOrDefault(DateTimeOffset.Now)
        };

        return packageMetadata;
    }

    public static Task<Result<IFile>> GetExecutable(IDirectory directory, FromDirectoryOptions setup)
    {
        return setup.ExecutableName.Match(s => ExecutableLookupByName(directory, s), () => ExecutableLookupWithoutName(directory));
    }

    public static Task<Result<Architecture>> GetArch(FromDirectoryOptions setup, IFile exec)
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

    private static async Task<Result<IIcon>> GetIcon(FromDirectoryOptions setup, IDirectory directory)
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

    private static async Task<Result<IFile>> ExecutableLookupByName(IDirectory directory, string execName)
    {
        var result = await Task.FromResult(directory.Files().TryFirst(x => x.Name == execName).ToResult($"Could not find executable file '{execName}'"));
        Log.Information("Looking up for executable named '{ExecName}'", execName);
        return result.Tap(() => Log.Information("Executable found successfully"));
    }

    private static async Task<Result<IFile>> ExecutableLookupWithoutName(IDirectory directory)
    {
        Log.Information("No executable has been specified. Looking up for candidates.");
        var execFiles = await directory.Files()
            .ToObservable()
            .Select(async file => (await file.IsElf()).Map(isElf => new { IsElf = isElf, File = file }))
            .Concat()
            .Successes()
            .Where(x => x.IsElf && !x.File.Name.EndsWith(".so") && x.File.Name != "createdump")
            .Select(x => x.File)
            .ToList();
        return execFiles
            .TryFirst()
            .ToResult("No executable has been specified Could not find any executable")
            .Tap(file => Log.Information("Choosing {Executable}", file));
    }
}