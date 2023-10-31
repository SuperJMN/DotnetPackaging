using System.Reactive.Linq;
using Archiver.Ar;
using SharpCompress;

namespace Archiver.Deb;

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
        var data = $"""
                        Package: {metadata.PackageName.ToLower()}
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
                        Description: open-source, non-custodial, privacy focused Bitcoin wallet
                          Built-in Tor, coinjoin, payjoin and coin control features.

                        """.FromCrLfToLf();

        var arProperties = new Ar.Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now,
            Length = data.Length,
        };
        
        return new EntryData("control.tar", arProperties, () =>
        {
            var tarProperties = new Tar.Properties
            {
                FileMode = FileMode.Parse("644"),
                GroupId = 1000,
                OwnerId = 1000,
                LastModification = DateTimeOffset.Now,
                Length = data.Length,
                GroupName = "root",
                OwnerUsername = "root"
            };

            return new Tar.TarFile(new Tar.EntryData("control", tarProperties, () => data.GetAsciiBytes().ToObservable())).Bytes;
        });
    }

    private EntryData Data()
    {
        var entries = contents.Entries.Select(tuple =>
        {
            return new Tar.EntryData(tuple.Item1, new Tar.Properties()
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = tuple.Item2().AsEnumerable().Count(),
                FileMode = FileMode.Parse("644"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
            }, tuple.Item2);
        });

        var tarEntry = new Tar.TarFile(entries.ToArray());

        var properties = new Properties()
        {
            Length = tarEntry.Bytes.ToEnumerable().Count(),
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            OwnerId = 1000,
            LastModification = DateTimeOffset.Now,
        };

        return new EntryData("data.tar", properties, () => tarEntry.Bytes);
    }

    private EntryData DebEntry()
    {
        var data = "debian-binary";

        var properties = new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now,
            Length = data.Length,
        };

        var contents = """
                      2.0

                      """.FromCrLfToLf();
        
        return new EntryData(data, properties, () => contents.GetAsciiBytes().ToObservable());
    }
}