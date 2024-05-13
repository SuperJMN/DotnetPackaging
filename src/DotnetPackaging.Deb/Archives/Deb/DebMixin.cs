using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Archives.Deb;

public static class DebMixin
{
    public static IData ToData(this DebFile debFile)
    {
        ArFile arFile = new ArFile(Signature(debFile), ControlTar(debFile), DataTar(debFile));
        return arFile.ToData();
    }

    private static Entry DataTar(DebFile debFile)
    {
        TarFile dataTarFile = new TarFile(debFile.Entries);
        var properties = new Properties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = 0,
            LastModification = debFile.Metadata.ModificationTime,
            OwnerId = 0,
        };
        return new Entry(new ByteProviderFile("data.tar", dataTarFile.ToData()), properties);
    }

    private static Entry Signature(DebFile debFile)
    {
        var properties = new Properties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = 0,
            LastModification = debFile.Metadata.ModificationTime,
            OwnerId = 0,
        };

        var signature = """
                        2.0

                        """.FromCrLfToLf();

        return new Entry(new File("debian-binary", signature), properties);
    }

    private static Entry ControlTar(DebFile debFile)
    {
        var properties = new Properties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = 0,
            LastModification = debFile.Metadata.ModificationTime,
            OwnerId = 0,
        };
        
        var controlTarFile = ControlTarFile(debFile);
        return new Entry(new ByteProviderFile("control.tar", controlTarFile.ToData()), properties);
    }
    
     private static TarFile ControlTarFile(DebFile deb)
    {
        var fileProperties = new TarFileProperties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = deb.Metadata.ModificationTime,
        };
        
        var dirProperties = new TarDirectoryProperties()
        {
            FileMode = "755".ToFileMode(),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = deb.Metadata.ModificationTime,
        };

        var content = $"""
                 Package: {deb.Metadata.Package}
                 Version: {deb.Metadata.Version}
                 Section: {deb.Metadata.Section}
                 Priority: {deb.Metadata.Priority}
                 Architecture: {deb.Metadata.Architecture.Name}
                 Maintainer: {deb.Metadata.Maintainer}
                 Description: {deb.Metadata.Description}

                 """;

        var file = new File("control", content.FromCrLfToLf());
        
        var entries = new TarEntry[]
        {
            new DirectoryTarEntry("./", dirProperties),
            new FileTarEntry("./control", file, fileProperties)
        };
        
        //Homepage: {deb.ControlMetadata.Homepage}
        //Vcs-Git: {deb.ControlMetadata.VcsGit}
        //Vcs-Browser: {deb.ControlMetadata.VcsBrowser}
        //License: {deb.ControlMetadata.License}
        //Installed-Size: {deb.ControlMetadata.InstalledSize}
        //Recommends: {deb.ControlMetadata.Recommends}
        
        var controlTarFile = new TarFile(entries.ToArray());
        return controlTarFile;
    }
}