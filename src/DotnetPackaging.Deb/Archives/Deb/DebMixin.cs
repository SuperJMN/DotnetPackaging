using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using Zafiro.Mixins;

namespace DotnetPackaging.Deb.Archives.Deb;

public static class DebMixin
{
    public static IByteSource ToData(this DebFile debFile) => debFile.ToByteSource();

    public static IByteSource ToByteSource(this DebFile debFile)
    {
        var arFile = new ArFile(Signature(debFile), ControlTar(debFile), DataTar(debFile));
        return arFile.ToByteSource();
    }

    private static ArEntry DataTar(DebFile debFile)
    {
        var dataTarFile = new TarFile(debFile.Entries);
        var properties = DefaultProperties(debFile.Metadata.ModificationTime);
        var content = dataTarFile.ToByteSource();
        return new ArEntry("data.tar", content, dataTarFile.Size(), properties);
    }

    private static ArEntry Signature(DebFile debFile)
    {
        var properties = DefaultProperties(debFile.Metadata.ModificationTime);
        var signature = "2.0\n";
        var signatureBytes = Encoding.ASCII.GetBytes(signature);
        return new ArEntry("debian-binary", ByteSource.FromBytes(signatureBytes), signatureBytes.Length, properties);
    }

    private static ArEntry ControlTar(DebFile debFile)
    {
        var properties = DefaultProperties(debFile.Metadata.ModificationTime);
        var controlTarFile = ControlTarFile(debFile);
        var content = controlTarFile.ToByteSource();
        return new ArEntry("control.tar", content, controlTarFile.Size(), properties);
    }

    private static TarFile ControlTarFile(DebFile deb)
    {
        var fileProperties = new TarFileProperties
        {
            Permissions = UnixPermissionHelper.FromOctal("644"),
            GroupId = 0,
            GroupName = "root",
            OwnerId = 0,
            OwnerUsername = "root",
            LastModification = deb.Metadata.ModificationTime,
        };

        var dirProperties = new TarDirectoryProperties
        {
            Permissions = UnixPermissionHelper.FromOctal("755"),
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
        var controlBytes = Encoding.ASCII.GetBytes(content);

        var entries = new TarEntry[]
        {
            new DirectoryTarEntry("./", dirProperties),
            new FileTarEntry("./control", controlBytes, fileProperties)
        };

        var controlTarFile = new TarFile(entries.ToArray());
        return controlTarFile;
    }

    private static ArEntryProperties DefaultProperties(DateTimeOffset modificationTime)
    {
        return new ArEntryProperties
        {
            Permissions = UnixPermissionHelper.FromOctal("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = modificationTime,
        };
    }
}
