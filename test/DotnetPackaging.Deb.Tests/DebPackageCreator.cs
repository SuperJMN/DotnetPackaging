using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests;

public static class DebPackageCreator
{
    public static async Task<Result<DebFile>> CreateFromDirectory(Directory directory, PackageMetadata metadata)
    {
        var appDir = $"/opt/{metadata.Package}";
        var filesInDirectory = await directory.GetFilesInTree(ZafiroPath.Empty);
        return filesInDirectory.Map(files =>
        {
            var tarFileProperties = RegularFileProperties();
            var executablePath = $"{appDir}/{metadata.ExecutableName}";
            var tarEntriesFromImplicitFiles = ImplicitFileEntries(metadata, executablePath, tarFileProperties);
            var tarEntriesFromFiles = TarEntriesFromFiles(files, metadata);
            var allFiles = tarEntriesFromFiles.Concat(tarEntriesFromImplicitFiles).ToList();
            var directoryEntries = CreateDirectoryEntries(allFiles);
            var tarEntries = directoryEntries.Concat(allFiles);
            return new DebFile(metadata, tarEntries.ToArray());
        });
    }

    private static TarFileProperties RegularFileProperties() => new()
    {
        FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
        GroupId = 1000,
        OwnerId = 1000,
        GroupName = "root",
        OwnerUsername = "root",
        LastModification = DateTimeOffset.Now
    };

    private static TarFileProperties ExecutableFileProperties() => RegularFileProperties() with { FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("755") };

    private static IEnumerable<FileTarEntry> ImplicitFileEntries(PackageMetadata metadata, string executablePath, TarFileProperties tarFileProperties)
    {
        return new FileTarEntry[]
        {
            new($"./usr/share/applications/{metadata.Package.ToLower()}.desktop", new StringByteProvider(MiscMixin.DesktopFileContents(executablePath, metadata), Encoding.ASCII), tarFileProperties),
            new($"./usr/bin/{metadata.Package.ToLower()}", new StringByteProvider(MiscMixin.RunScript(executablePath), Encoding.ASCII), tarFileProperties)
        }.Concat(GetIconEntry(metadata));
    }

    private static IEnumerable<FileTarEntry> GetIconEntry(PackageMetadata metadata)
    {
        if (metadata.Icon.HasNoValue)
        {
            return Enumerable.Empty<FileTarEntry>();
        }

        return [new FileTarEntry($"./usr/share/icons/{metadata.Package}", metadata.Icon.Value, RegularFileProperties())];
    }

    private static IEnumerable<TarEntry> TarEntriesFromFiles(IEnumerable<IRootedFile> files, PackageMetadata metadata)
    {
        return files.Select(x => new FileTarEntry($"./opt/{metadata.Package}/" + x.FullPath(), x.Rooted, GetFileProperties(x, metadata.ExecutableName)));
    }

    private static TarFileProperties GetFileProperties(IRootedFile file, string executableName)
    {
        if (string.Equals(file.Name, executableName, StringComparison.Ordinal))
        {
            return ExecutableFileProperties();
        }

        return RegularFileProperties();
    }

    private static IEnumerable<TarEntry> CreateDirectoryEntries(IEnumerable<TarEntry> allFiles)
    {
        var directoryProperties = new TarDirectoryProperties
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
            GroupId = 1000,
            OwnerId = 1000,
            GroupName = "root",
            OwnerUsername = "root",
            LastModification = DateTimeOffset.Now
        };
        var directoryEntries = allFiles.Select(x => ((ZafiroPath)x.Path[2..]).Parents()).Flatten().Distinct().OrderBy(x => x.RouteFragments.Count());
        return directoryEntries.Select(path => new DirectoryTarEntry($"./{path}", directoryProperties));
    }
}