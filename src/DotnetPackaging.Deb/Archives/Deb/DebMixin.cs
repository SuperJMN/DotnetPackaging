using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using DotnetPackaging.Deb.Bytes;
using DotnetPackaging.Deb.Unix;

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
            GroupId = Maybe<int>.From(0),
            LastModification = debFile.Metadata.ModificationTime,
            OwnerId = Maybe<int>.From(0),
        };
        return new Entry("data.tar", dataTarFile.ToData(), properties);
    }

    private static Entry Signature(DebFile debFile)
    {
        var properties = new Properties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = Maybe<int>.From(0),
            LastModification = debFile.Metadata.ModificationTime,
            OwnerId = Maybe<int>.From(0),
        };

        var signature = """
                        2.0

                        """.FromCrLfToLf();

        return new Entry("debian-binary", Data.FromString(signature), properties);
    }

    private static Entry ControlTar(DebFile debFile)
    {
        var properties = new Properties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = Maybe<int>.From(0),
            LastModification = debFile.Metadata.ModificationTime,
            OwnerId = Maybe<int>.From(0),
        };

        var controlTarFile = ControlTarFile(debFile);
        return new Entry("control.tar", controlTarFile.ToData(), properties);
    }

    private static TarFile ControlTarFile(DebFile deb)
    {
        var fileProperties = new TarFileProperties()
        {
            FileMode = "644".ToFileMode(),
            GroupId = Maybe<int>.From(0),
            GroupName = Maybe<string>.From("root"),
            OwnerId = Maybe<int>.From(0),
            OwnerUsername = Maybe<string>.From("root"),
            LastModification = deb.Metadata.ModificationTime,
        };

        var dirProperties = new TarDirectoryProperties()
        {
            FileMode = "755".ToFileMode(),
            GroupId = Maybe<int>.From(0),
            GroupName = Maybe<string>.From("root"),
            OwnerId = Maybe<int>.From(0),
            OwnerUsername = Maybe<string>.From("root"),
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

        var fileContent = Data.FromString(content);

        var entries = new TarEntry[]
        {
            new DirectoryTarEntry("./", dirProperties),
            new FileTarEntry("./control", fileContent, fileProperties)
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
}
