using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;

namespace DotnetPackaging.Deb.Tests;

public static class DebPackageCreator
{
    public static async Task<Result<DebFile>> CreateFromDirectory(Directory directory, PackageMetadata metadata)
    {
        var appDir = $"/opt/{metadata.Package}";
        var filesInDirectory = await directory.GetFilesInTree(ZafiroPath.Empty);
        return filesInDirectory.Map(files =>
        {
            var tarFileProperties = CreateTarFileProperties();
            var executablePath = $"{appDir}/{metadata.ExecutableName}";
            var additionalFileEntries = CreateAdditionalFileEntries(metadata, executablePath, tarFileProperties);
            var directoryFileEntries = CreateDirectoryFileEntries(files, metadata, tarFileProperties);
            var allFiles = directoryFileEntries.Concat(additionalFileEntries).ToList();
            var directoryEntries = CreateDirectoryEntries(allFiles);
            var tarEntries = directoryEntries.Concat(allFiles);
            return new DebFile(metadata, tarEntries.ToArray());
        });
    }

    private static TarFileProperties CreateTarFileProperties() => new()
    {
        FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
        GroupId = 1000,
        OwnerId = 1000,
        GroupName = "root",
        OwnerUsername = "root",
        LastModification = DateTimeOffset.Now
    };

    private static FileTarEntry[] CreateAdditionalFileEntries(PackageMetadata metadata, string executablePath, TarFileProperties tarFileProperties)
    {
        return new FileTarEntry[]
        {
            new($"./usr/share/{metadata.Package.ToLower()}.desktop", new StringByteProvider(MiscMixin.DesktopFileContents(executablePath, metadata), Encoding.ASCII), tarFileProperties),
            new($"./usr/bin/{metadata.Package.ToLower()}", new StringByteProvider(MiscMixin.RunScript(executablePath), Encoding.ASCII), tarFileProperties)
        };
    }

    private static IEnumerable<TarEntry> CreateDirectoryFileEntries(IEnumerable<IRootedFile> files, PackageMetadata metadata, TarFileProperties tarFileProperties)
    {
        return files.Select(x => new FileTarEntry($"./opt/{metadata.Package}/" + x.FullPath(), x.Rooted, tarFileProperties));
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
        var directoryEntries = allFiles.Select(x => ((ZafiroPath) x.Path[2..]).Parents()).Flatten().Distinct().OrderBy(x => x.RouteFragments.Count());
        return directoryEntries.Select(path => new DirectoryTarEntry($"./{path}", directoryProperties));
    }
}