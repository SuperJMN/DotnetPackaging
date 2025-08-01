﻿using System.IO.Abstractions;
using System.Reactive.Linq;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem.Readonly;
using IDirectory = Zafiro.FileSystem.Readonly.IDirectory;
using IFile = Zafiro.FileSystem.Readonly.IFile;

namespace DotnetPackaging;

public static class BuildUtils
{
    public static async Task<PackageMetadata> CreateMetadata(FromDirectoryOptions setup, IDirectory directory, Architecture architecture, IFile exec, bool isTerminal)
    {
        var icon = await GetIcon(setup, directory).TapError(Log.Warning);
        var package = setup.Package.Or(setup.Name).GetValueOrDefault(exec.Name.Replace(".Desktop", ""));
        var versionResult = await setup.Version
            .Match(v => Task.FromResult(Result.Success(v)), () => GitVersionRunner.Run());
        var version = versionResult.GetValueOrDefault("1.0.0");
        var name = setup.Name.GetValueOrDefault(directory.Name);
        
        var packageMetadata = new PackageMetadata(name, architecture, isTerminal, package, version)
        {
            Architecture = architecture,
            Icon = FunctionalMixin.AsMaybe(icon),
            Id = setup.Id,
            Name = name,
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
            Package = package,
            Version = version,
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
            .Or(async () =>
            {
                var architecture = await exec.GetArchitecture();
                return architecture.MapError(err => $"Invalid architecture of file \"{exec}\": {err}");
            })
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
            .Select(file => Observable.FromAsync(async () => await file.IsElf()).Map(isElf => new { IsElf = isElf, File = file }))
            .Merge(3)
            .Successes()
            .Where(x => x.IsElf && !x.File.Name.EndsWith(".so") && x.File.Name != "createdump")
            .Select(x => x.File)
            .ToList();
        return execFiles
            .TryFirst()
            .ToResult(@$"Could not find any executable file in the input folder ""{directory}""")
            .Tap(file => Log.Information("Choosing {Executable}", file));
    }
}