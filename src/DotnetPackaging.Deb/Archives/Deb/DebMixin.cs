using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Deb;

public static class DebMixin
{
    public static IByteSource ToByteSource(this DebFile debFile)
    {
        ArFile arFile = new ArFile(Signature(debFile), ControlTar(debFile), DataTar(debFile));
        return arFile.ToByteSource();
    }

    private static ArEntry DataTar(DebFile debFile)
    {
        TarFile dataTarFile = new TarFile(debFile.Entries);
        var properties = DefaultProperties(debFile);
        return new ArEntry("data.tar", dataTarFile.ToByteSource(), properties);
    }

    private static ArEntry Signature(DebFile debFile)
    {
        var properties = DefaultProperties(debFile);

        var signature = "2.0\n";

        return new ArEntry("debian-binary", ByteSource.FromString(signature), properties);
    }

    private static ArEntry ControlTar(DebFile debFile)
    {
        var properties = DefaultProperties(debFile);
        var controlTarFile = ControlTarFile(debFile);
        return new ArEntry("control.tar", controlTarFile.ToByteSource(), properties);
    }

    private static TarFile ControlTarFile(DebFile deb)
    {
        var fileProperties = new TarFileProperties()
        {
            Mode = UnixPermissions.Parse("644"),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = deb.Metadata.ModificationTime,
        };

        var dirProperties = new TarDirectoryProperties()
        {
            Mode = UnixPermissions.Parse("755"),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = deb.Metadata.ModificationTime,
        };

        var items = new[]
        {
            Maybe.From(deb.Metadata.Package).Map(s => $"Package: {s}"),
            Maybe.From(deb.Metadata.Version).Map(s => $"Version: {s}"),
            Maybe.From(deb.Metadata.Architecture).Map(s => $"Architecture: {s.Name}"),
            deb.Metadata.Section.Map(s => $"Section: {s}"),
            deb.Metadata.Priority.Map(s => $"Priority: {s}"),
            deb.Metadata.Maintainer.Map(s => $"Maintainer: {s}"),
            deb.Metadata.Description.Map(s => $"Description: {s}"),
        };

        var content = items.Compose() + "\n";

        var file = ByteSource.FromString(content);

        var entries = new TarEntry[]
        {
            new DirectoryTarEntry("./", dirProperties),
            new FileTarEntry("./control", file, fileProperties)
        };

        // TODO: Add other properties, too
        //Homepage: {deb.ControlMetadata.Homepage}
        //Vcs-Git: {deb.ControlMetadata.VcsGit}
        //Vcs-Browser: {deb.ControlMetadata.VcsBrowser}
        //License: {deb.ControlMetadata.License}
        //Installed-Size: {deb.ControlMetadata.InstalledSize}
        //Recommends: {deb.ControlMetadata.Recommends}

        var controlTarFile = new TarFile(entries.ToArray());
        return controlTarFile;
    }

    private static UnixFileProperties DefaultProperties(DebFile debFile)
    {
        return new UnixFileProperties
        {
            Mode = UnixPermissions.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            GroupName = "root",
            OwnerUsername = "root",
            LastModification = debFile.Metadata.ModificationTime
        };
    }
}
