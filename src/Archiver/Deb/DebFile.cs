using System.Reactive.Linq;
using Archiver.Ar;

namespace Archiver.Deb;

public class DebFile
{
    private readonly Metadata metadata;

    public DebFile(Metadata metadata)
    {
        this.metadata = metadata;
    }

    public IObservable<byte> Bytes
    {
        get
        {
            var debEntry = DebEntry();
            var control = Control();

            return Observable.Concat(debEntry.Bytes, control.Bytes);
        }
    }

    private Entry Control()
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
        
        return new Entry(new EntryData("control.tar", arProperties, () =>
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
        }));
    }

    private Entry Data()
    {
        throw new NotImplementedException();

        //var properties = new Properties
        //{
        //    FileMode   = FileMode.Parse("644"),
        //    GroupId = 0,
        //    OwnerId = 0,
        //    LastModification = DateTimeOffset.Now
        //};

        //return new Entry(new EntryData("data.tar", properties, () => new Tar.TarFile())));
    }

    private Entry DebEntry()
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
        
        return new Entry(new EntryData(data, properties, () => contents.GetAsciiBytes().ToObservable()));
    }
}