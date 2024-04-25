using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions.Extensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests;

public class DebFileWriter
{
    public static async Task<Result> Write(DebFile deb, MemoryStream stream)
    {
        var data = $"""
                    Package: {deb.ControlMetadata.Package}
                    Priority: {deb.ControlMetadata.Priority}
                    Section: {deb.ControlMetadata.Section}
                    Maintainer: {deb.ControlMetadata.Maintainer}
                    Version: {deb.ControlMetadata.Version}
                    Homepage: {deb.ControlMetadata.Homepage}
                    Vcs-Git: {deb.ControlMetadata.VcsGit}
                    Vcs-Browser: {deb.ControlMetadata.VcsBrowser}
                    Architecture: {deb.ControlMetadata.Architecture}
                    License: {deb.ControlMetadata.License}
                    Installed-Size: {deb.ControlMetadata.InstalledSize}
                    Recommends: {deb.ControlMetadata.Recommends}
                    Description: {deb.ControlMetadata.Description}
                    """.FromCrLfToLf();

        var signature = """
                       2.0

                       """.FromCrLfToLf();

        var properties = new Properties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("644", 8),
            GroupId = 0,
            LastModification = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22),
            OwnerId = 0,
        };

        var unixFileProperties = new UnixFileProperties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("322", 8),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = DateTimeOffset.Now,
            LinkIndicator = 1
        };

        var entries = new FileTarEntry[]
        {
            new(new RootedFile(ZafiroPath.Empty,new File("control", TestMixin.String(signature))), unixFileProperties)
        };
        
        var filePaths = entries.Select(x => x.File.FullPath());
        var dirs = filePaths.DirectoryPaths().OrderBy(x => x.RouteFragments.Count());
        var directoryTarEntries = dirs.Select(path => (TarEntry)new DirectoryTarEntry(path, unixFileProperties));
        var tarEntries = directoryTarEntries.Concat(entries);
        var controlTarFile = new TarFile(tarEntries.ToArray());

        var controlFile =
            new ArFile
            (
                new Entry(new File("debian-binary", TestMixin.String(signature)), properties),
                new Entry(new File("control.tar", () => controlTarFile.ToStream()), properties)
            );

        await ArWriter.Write(controlFile, stream);

        return Result.Success();
    }
}