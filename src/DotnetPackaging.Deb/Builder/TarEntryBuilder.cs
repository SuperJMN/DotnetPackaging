using System.Text;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DivineBytes;
using DivinePath = Zafiro.DivineBytes.Path;

namespace DotnetPackaging.Deb.Builder;

internal static class TarEntryBuilder
{
    public static IEnumerable<TarEntry> From(IContainer container, PackageMetadata metadata, INamedByteSourceWithPath executable)
    {
        var appDir = $"/opt/{metadata.Package}";
        var executableRelativePath = executable.Path == DivinePath.Empty ? executable.Name : executable.FullPath().ToString();
        var execAbsolutePath = $"{appDir}/{executableRelativePath}".Replace("\\", "/", StringComparison.Ordinal);
        var tarEntriesFromImplicitFiles = ImplicitFileEntries(metadata, execAbsolutePath);
        var tarEntriesFromFiles = TarEntriesFromFiles(container, metadata, executable);
        var allFiles = tarEntriesFromFiles.Concat(tarEntriesFromImplicitFiles).ToList();
        var directoryEntries = CreateDirectoryEntries(allFiles);
        return directoryEntries.Concat(allFiles);
    }

    private static IEnumerable<FileTarEntry> ImplicitFileEntries(PackageMetadata metadata, string executablePath)
    {
        return new FileTarEntry[]
        {
            new($"./usr/share/applications/{metadata.Package.ToLowerInvariant()}.desktop", ByteSource.FromString(TextTemplates.DesktopFileContents(executablePath, metadata), Encoding.ASCII), Misc.RegularFileProperties()),
            new($"./usr/bin/{metadata.Package.ToLowerInvariant()}", ByteSource.FromString(TextTemplates.RunScript(executablePath), Encoding.ASCII), Misc.ExecutableFileProperties())
        }.Concat(GetIconEntries(metadata));
    }

    private static IEnumerable<FileTarEntry> GetIconEntries(PackageMetadata metadata)
    {
        return metadata.IconFiles.Select(icon => new FileTarEntry(
            NormalizeTarPath($"./{icon.Key}"),
            icon.Value,
            Misc.RegularFileProperties()));
    }

    private static IEnumerable<TarEntry> TarEntriesFromFiles(IContainer container, PackageMetadata metadata, INamedByteSourceWithPath executable)
    {
        var prefix = $"./opt/{metadata.Package}";
        return container.ResourcesWithPathsRecursive()
            .Select(resource => new FileTarEntry(
                NormalizeTarPath($"{prefix}/{RelativePath(resource)}"),
                resource,
                GetFileProperties(resource, executable)));
    }

    private static string RelativePath(INamedByteSourceWithPath resource)
    {
        return resource.Path == DivinePath.Empty ? resource.Name : resource.FullPath().ToString();
    }

    private static string NormalizeTarPath(string path)
    {
        var normalized = path.Replace("\\", "/");
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.StartsWith('/') ? $".{normalized}" : $"./{normalized}";
        }

        return normalized;
    }

    private static TarFileProperties GetFileProperties(INamedByteSourceWithPath file, INamedByteSourceWithPath executable)
    {
        var isExecutable = string.Equals(file.Name, executable.Name, StringComparison.Ordinal)
                           && file.Path.Value == executable.Path.Value;

        return isExecutable ? Misc.ExecutableFileProperties() : Misc.RegularFileProperties();
    }

    private static IEnumerable<TarEntry> CreateDirectoryEntries(IEnumerable<TarEntry> allFiles)
    {
        var directoryProperties = new TarDirectoryProperties
        {
            Mode = UnixPermissions.Parse("755"),
            GroupId = 1000,
            OwnerId = 1000,
            GroupName = "root",
            OwnerUsername = "root",
            LastModification = DateTimeOffset.Now
        };

        var directories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in allFiles)
        {
            var route = entry.Path.TrimStart('.');
            route = route.TrimStart('/');

            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                continue;
            }

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
}
