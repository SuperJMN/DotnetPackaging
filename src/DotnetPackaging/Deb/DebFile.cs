using System.Reactive.Linq;
using DotnetPackaging.Ar;
using DotnetPackaging.Common;
using DotnetPackaging.Tar;
using Zafiro.FileSystem;
using EntryData = DotnetPackaging.Ar.EntryData;
using Properties = DotnetPackaging.Ar.Properties;

namespace DotnetPackaging.Deb;

public class DebFile
{
    private readonly Metadata metadata;
    private readonly Contents contents;

    public DebFile(Metadata metadata, Contents contents)
    {
        this.metadata = metadata;
        this.contents = contents;
    }

    public IObservable<byte> Bytes => new ArFile(DebEntry(), Control(), Data()).Bytes;

    private EntryData Control()
    {
        var tarFile = ControlTar();

        var arProperties = new Ar.Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now,
            Length = tarFile.Length,
        };

        return new EntryData("control.tar", arProperties, () => tarFile.Bytes);
    }

    public TarFile ControlTar()
    {
        var data = $"""
                    Package: {metadata.PackageName}
                    Priority: optional
                    Section: utils
                    Maintainer: {metadata.Maintainer}
                    Version: 2.0.4
                    Homepage: {metadata.Homepage}
                    Vcs-Git: git://github.com/zkSNACKs/WalletWasabi.git
                    Vcs-Browser: https://github.com/zkSNACKs/WalletWasabi
                    Architecture: {metadata.Architecture}
                    License: {metadata.License}
                    Installed-Size: 207238
                    Recommends: policykit-1
                    Description: {metadata.Description}

                    """.FromCrLfToLf();

        var tarProperties = new Tar.Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            OwnerId = 1000,
            LastModification = DateTimeOffset.Now,
            Length = data.Length,
            GroupName = "root",
            OwnerUsername = "root",
            LinkIndicator = 0,
        };

        var dir = DirEntry("./");
        return new TarFile(dir, new Tar.EntryData("./control", tarProperties, () => data.GetAsciiBytes().ToObservable()));
    }

    private static Tar.EntryData DirEntry(string path) => new(path, new Tar.Properties()
    {
        Length = 0,
        GroupName = "root",
        OwnerUsername = "root",
        GroupId = 1000,
        OwnerId = 1000,
        FileMode = FileMode.Parse("644"),
        LastModification = DateTimeOffset.Now,
        LinkIndicator = 5,
    }, Observable.Empty<byte>);

    private EntryData Data()
    {
        var dataTar = DataTar();

        var properties = new Properties()
        {
            Length = dataTar.Bytes.ToEnumerable().Count(),
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            OwnerId = 1000,
            LastModification = DateTimeOffset.Now,
        };

        return new EntryData("data.tar", properties, () => dataTar.Bytes);
    }

    public TarFile DataTar()
    {
        var fileEntries = contents.Entries.Select(tuple =>
        {
            var path = ZafiroPath.Create($"./usr/local/bin/{metadata.PackageName}").Value.Combine(tuple.Item1);

            return new Tar.EntryData(path, new Tar.Properties()
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = tuple.Item2().ToEnumerable().Count(),
                FileMode = FileMode.Parse("644"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
                LinkIndicator = 0
            }, tuple.Item2);
        });

        var dirEntries = new DebPaths(metadata.PackageName, contents.Entries.Select(x => x.Item1))
            .Directories()
            .Select(path => path + "/")
            .OrderBy(x => x.Length)
            .Select(DirEntry);
        
        return new TarFile(dirEntries.Concat(fileEntries).ToArray());
    }

    private EntryData DebEntry()
    {
        var data = "debian-binary";

        var contents = """
                       2.0

                       """.FromCrLfToLf();

        var properties = new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now,
            Length = contents.Length,
        };
        
        return new EntryData(data, properties, () => contents.GetAsciiBytes().ToObservable());
    }
}