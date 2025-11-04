using System.Text;
using Zafiro.DataModel;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Unix;
using DivinePath = Zafiro.DivineBytes.Path;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmLayoutBuilder
{
    public static IReadOnlyList<RpmEntry> Build(IContainer container, PackageMetadata metadata, INamedByteSourceWithPath executable)
    {
        var entries = new List<RpmEntry>();
        var appDir = $"/opt/{metadata.Package}";
        var executableRelativePath = executable.Path == DivinePath.Empty ? executable.Name : executable.FullPath().ToString();
        var execAbsolutePath = NormalizePath($"{appDir}/{executableRelativePath}");

        entries.AddRange(FilesFromContainer(container, metadata, executable, appDir));
        entries.AddRange(ImplicitFiles(metadata, execAbsolutePath));

        var directoryEntries = BuildDirectoryEntries(entries);
        return directoryEntries.Concat(entries).ToList();
    }

    private static IEnumerable<RpmEntry> FilesFromContainer(IContainer container, PackageMetadata metadata, INamedByteSourceWithPath executable, string appDir)
    {
        return container.ResourcesWithPathsRecursive()
            .Select(resource =>
            {
                var relative = resource.Path == DivinePath.Empty ? resource.Name : resource.FullPath().ToString();
                var path = NormalizePath($"{appDir}/{relative}");
                var data = Data.FromByteArray(resource.Array());
                var properties = GetFileProperties(resource, executable);
                return new RpmEntry(path, properties, data, RpmEntryType.File);
            });
    }

    private static IEnumerable<RpmEntry> ImplicitFiles(PackageMetadata metadata, string execAbsolutePath)
    {
        var result = new List<RpmEntry>();
        var desktopPath = NormalizePath($"/usr/share/applications/{metadata.Package.ToLowerInvariant()}.desktop");
        var desktopContent = Data.FromString(TextTemplates.DesktopFileContents(execAbsolutePath, metadata), Encoding.ASCII);
        result.Add(new RpmEntry(desktopPath, UnixFileProperties.RegularFileProperties(), desktopContent, RpmEntryType.File));

        var launcherPath = NormalizePath($"/usr/bin/{metadata.Package.ToLowerInvariant()}");
        var launcherContent = Data.FromString(TextTemplates.RunScript(execAbsolutePath), Encoding.ASCII);
        result.Add(new RpmEntry(launcherPath, UnixFileProperties.ExecutableFileProperties(), launcherContent, RpmEntryType.File));

        foreach (var icon in metadata.IconFiles)
        {
            var iconPath = NormalizePath($"/{icon.Key}");
            var iconData = Data.FromByteArray(icon.Value.Array());
            result.Add(new RpmEntry(iconPath, UnixFileProperties.RegularFileProperties(), iconData, RpmEntryType.File));
        }

        return result;
    }

    private static IEnumerable<RpmEntry> BuildDirectoryEntries(IEnumerable<RpmEntry> entries)
    {
        var directoryProperties = UnixFileProperties.RegularDirectoryProperties();

        var directories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.Type != RpmEntryType.File)
            {
                continue;
            }

            AddParentDirectories(entry.Path, directories);
        }

        return directories
            .OrderBy(path => path.Count(c => c == '/'))
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(path => new RpmEntry(path, directoryProperties, null, RpmEntryType.Directory));
    }

    private static void AddParentDirectories(string path, ISet<string> directories)
    {
        var trimmed = path.TrimStart('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return;
        }

        var current = string.Empty;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            current = string.IsNullOrEmpty(current) ? $"/{segments[i]}" : $"{current}/{segments[i]}";
            directories.Add(current);
        }
    }

    private static UnixFileProperties GetFileProperties(INamedByteSourceWithPath file, INamedByteSourceWithPath executable)
    {
        var isExecutable = string.Equals(file.Name, executable.Name, StringComparison.Ordinal)
                           && file.Path.Value == executable.Path.Value;

        return isExecutable ? UnixFileProperties.ExecutableFileProperties() : UnixFileProperties.RegularFileProperties();
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace("\\", "/", StringComparison.Ordinal);
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = $"/{normalized.TrimStart('.')}";
        }

        return normalized;
    }
}
