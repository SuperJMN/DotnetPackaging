using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests;

public class DebFileWriter
{
    public static async Task<Result> Write(DebFile deb, MemoryStream stream)
    {
        var arFile = new ArFile(Signature(deb), ControlTar(deb));

        await ArWriter.Write(arFile, stream);

        return Result.Success();
    }

    private static Entry DataTar(DebFile deb)
    {
        var properties = new Properties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("644", 8),
            GroupId = 0,
            LastModification = deb.ControlMetadata.ModificationTime,
            OwnerId = 0,
        };
        
        var dataTar = ControlTarFile(deb);
        return new Entry(new File("control.tar", () => dataTar.ToStream()), properties);
    }

    private static Entry ControlTar(DebFile debFile)
    {
        var properties = new Properties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("644", 8),
            GroupId = 0,
            LastModification = debFile.ControlMetadata.ModificationTime,
            OwnerId = 0,
        };
        
        var controlTarFile = ControlTarFile(debFile);
        return new Entry(new File("control.tar", () => controlTarFile.ToStream()), properties);
    }

    private static TarFile DataTarFile(DebFile debFile)
    {
        //return new TarFile(debFile.Data.Select(entry => new FileE
        //ntry(entry.File, )))
        throw new NotImplementedException();
    }

    private static Entry Signature(DebFile debFile)
    {
        var properties = new Properties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("644", 8),
            GroupId = 0,
            LastModification = debFile.ControlMetadata.ModificationTime,
            OwnerId = 0,
        };
        
        var signature = """
                        2.0

                        """.FromCrLfToLf();

        return new Entry(new File("debian-binary", TestMixin.String(signature)), properties);
    }

    private static TarFile ControlTarFile(DebFile deb)
    {
        var fileProperties = new TarFileProperties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("644", 8),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = 24.April(2024).AddHours(12).AddMinutes(11).AddSeconds(36).ToDateTimeOffset(),
        };
        
        var dirProperties = new TarDirectoryProperties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("755", 8),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = 24.April(2024).AddHours(12).AddMinutes(11).AddSeconds(36).ToDateTimeOffset(),
        };


        var entries = new FileTarEntry[]
        {
            new(new RootedFile(ZafiroPath.Empty,new File("control", TestMixin.String($"""
                                                                                      Package: {deb.ControlMetadata.Package}
                                                                                      Version: {deb.ControlMetadata.Version}
                                                                                      Section: {deb.ControlMetadata.Section}
                                                                                      Priority: {deb.ControlMetadata.Priority}
                                                                                      Architecture: {deb.ControlMetadata.Architecture}
                                                                                      Maintainer: {deb.ControlMetadata.Maintainer}
                                                                                      Description: {deb.ControlMetadata.Description}

                                                                                      """.FromCrLfToLf()))), fileProperties)
        };
        
        //Homepage: {deb.ControlMetadata.Homepage}
        //Vcs-Git: {deb.ControlMetadata.VcsGit}
        //Vcs-Browser: {deb.ControlMetadata.VcsBrowser}
        //License: {deb.ControlMetadata.License}
        //Installed-Size: {deb.ControlMetadata.InstalledSize}
        //Recommends: {deb.ControlMetadata.Recommends}
        
        var filePaths = entries.Select(x => x.File.FullPath());
        var dirs = filePaths.DirectoryPaths().OrderBy(x => x.RouteFragments.Count());
        var directoryTarEntries = dirs.Select(path => (TarEntry)new DirectoryTarEntry(path, dirProperties));
        var tarEntries = directoryTarEntries.Concat(entries);
        var controlTarFile = new TarFile(tarEntries.ToArray());
        return controlTarFile;
    }
}