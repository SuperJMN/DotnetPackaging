﻿using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Mixins;

namespace DotnetPackaging.Deb.Tests;

public static class DebPackageCreator
{
    public static async Task<Result<DebFile>> CreateFromDirectory(IDirectory directory, PackageMetadata metadata)
    {
        var appDir = $"/opt/{metadata.Package}";
        var filesInDirectory = await directory.GetFilesInTree(ZafiroPath.Empty);
        return filesInDirectory.Map(files =>
        {
            var executablePath = $"{appDir}/{metadata.ExecutableName}";
            var tarEntriesFromImplicitFiles = ImplicitFileEntries(metadata, executablePath);
            var tarEntriesFromFiles = TarEntriesFromFiles(files, metadata);
            var allFiles = tarEntriesFromFiles.Concat(tarEntriesFromImplicitFiles).ToList();
            var directoryEntries = CreateDirectoryEntries(allFiles);
            var tarEntries = directoryEntries.Concat(allFiles);
            return new DebFile(metadata, tarEntries.ToArray());
        });
    }


    private static IEnumerable<FileTarEntry> ImplicitFileEntries(PackageMetadata metadata, string executablePath)
    {
        return new FileTarEntry[]
        {
            new($"./usr/local/share/applications/{metadata.Package.ToLower()}.desktop", new StringObservableDataStream(MiscMixin.DesktopFileContents(executablePath, metadata), Encoding.ASCII), Misc.RegularFileProperties()),
            new($"./usr/local/bin/{metadata.Package.ToLower()}", new StringObservableDataStream(MiscMixin.RunScript(executablePath), Encoding.ASCII), Misc.ExecutableFileProperties())
        }.Concat(GetIconEntry(metadata));
    }

    private static IEnumerable<FileTarEntry> GetIconEntry(PackageMetadata metadata)
    {
        if (metadata.Icon.HasNoValue)
        {
            return Enumerable.Empty<FileTarEntry>();
        }

        var size = metadata.Icon.Value.Size;
        return [new FileTarEntry($"./usr/local/share/icons/apps/{size}x{size}/{metadata.Package}.png", metadata.Icon.Value, Misc.RegularFileProperties())];
    }

    private static IEnumerable<TarEntry> TarEntriesFromFiles(IEnumerable<IRootedFile> files, PackageMetadata metadata)
    {
        return files.Select(x => new FileTarEntry($"./opt/{metadata.Package}/" + x.FullPath(), x.Rooted, GetFileProperties(x, metadata.ExecutableName)));
    }

    private static TarFileProperties GetFileProperties(IRootedFile file, string executableName)
    {
        if (string.Equals(file.Name, executableName, StringComparison.Ordinal))
        {
            return Misc.ExecutableFileProperties();
        }

        return Misc.RegularFileProperties();
    }

    private static IEnumerable<TarEntry> CreateDirectoryEntries(IEnumerable<TarEntry> allFiles)
    {
        var directoryProperties = new TarDirectoryProperties
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("755"),
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