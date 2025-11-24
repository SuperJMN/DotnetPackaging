using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DivineBytes;
using DotnetPackaging.Deb.Bytes;
using DotnetPackaging.Deb.Unix;

namespace DotnetPackaging.Flatpak;

public static class FlatpakBundle
{
    // Legacy simple tar of layout
    public static Result<IByteSource> Create(FlatpakBuildPlan plan)
    {
        var entries = ToTarEntries(plan);
        var tar = new TarFile(entries.ToArray());
        var data = tar.ToData();
        return Result.Success(ByteSource.FromByteObservable(data.Bytes));
    }

    // Experimental: create an OSTree repo and bundle it (still unsigned)
    public static Result<IByteSource> CreateOstree(FlatpakBuildPlan plan)
    {
        var repo = Ostree.OstreeRepoBuilder.Build(plan);
        if (repo.IsFailure) return Result.Failure<IByteSource>(repo.Error);
        // Tar the repo directory as a single file .flatpak
        var tarEntries = new List<DotnetPackaging.Deb.Archives.Tar.TarEntry>();
        foreach (var res in repo.Value.ResourcesWithPathsRecursive())
        {
            var p = $"./{((INamedWithPath)res).FullPath()}";
            var props = DotnetPackaging.Deb.Archives.Tar.Misc.RegularFileProperties();
            tarEntries.Add(new DotnetPackaging.Deb.Archives.Tar.FileTarEntry(p, Data.FromByteArray(res.Array()), props));
        }
        var tar = new DotnetPackaging.Deb.Archives.Tar.TarFile(tarEntries.ToArray());
        var data = tar.ToData();
        return Result.Success(ByteSource.FromByteObservable(data.Bytes));
    }

    private static IEnumerable<TarEntry> ToTarEntries(FlatpakBuildPlan plan)
    {
        var files = new List<FileTarEntry>();
        foreach (var res in plan.ToRootContainer().ResourcesWithPathsRecursive())
        {
            var path = NormalizeTarPath($"./{((INamedWithPath)res).FullPath()}");
            var baseProps = IsExecutable(plan, res) ? Misc.ExecutableFileProperties() : Misc.RegularFileProperties();
            var props = baseProps with { OwnerId = 0, GroupId = 0, OwnerUsername = "root", GroupName = "root" };
            files.Add(new FileTarEntry(path, Data.FromByteArray(res.Array()), props));
        }

        var dirs = CreateDirectoryEntries(files.Select(f => (TarEntry)f));
        return dirs.Concat(files);
    }

    private static bool IsExecutable(FlatpakBuildPlan plan, INamedByteSourceWithPath res)
    {
        var fullPath = ((INamedWithPath)res).FullPath().ToString().Replace("\\", "/");
        return string.Equals(fullPath, $"files/{plan.ExecutableTargetPath}", StringComparison.Ordinal)
               || fullPath.EndsWith($"/bin/{plan.CommandName}", StringComparison.Ordinal);
    }

    private static IEnumerable<TarEntry> CreateDirectoryEntries(IEnumerable<TarEntry> allFiles)
    {
        var directoryProperties = new TarDirectoryProperties
        {
            FileMode = "755".ToFileMode(),
            GroupId = 0,
            OwnerId = 0,
            GroupName = "root",
            OwnerUsername = "root",
            LastModification = DateTimeOffset.Now
        };

        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in allFiles)
        {
            var route = entry.Path.TrimStart('.');
            route = route.TrimStart('/');
            if (string.IsNullOrWhiteSpace(route)) continue;

            var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) continue;

            var current = string.Empty;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                current = string.IsNullOrEmpty(current) ? $"./{segments[i]}" : $"{current}/{segments[i]}";
                directories.Add(current);
            }
        }

        return directories
            .OrderBy(path => path.Count(c => c == '/'))
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(path => new DirectoryTarEntry(path, directoryProperties));
    }

    private static string NormalizeTarPath(string path)
    {
        var normalized = path.Replace("\\", "/");
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }
        return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized : $"./{normalized.TrimStart('/')}";
    }
}
