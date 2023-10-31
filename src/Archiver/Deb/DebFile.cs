using System.Reactive.Linq;
using Archiver.Ar;
using Zafiro.IO;

namespace Archiver.Deb;

public class DebFile
{
    private readonly Stream stream;
    private readonly Metadata metadata;
    private readonly Contents dataContents;

    public DebFile(Stream stream, Metadata metadata, Contents dataContents)
    {
        this.stream = stream;
        this.metadata = metadata;
        this.dataContents = dataContents;
    }

    public async Task Build()
    {
        var debEntry = DebEntry();
        var control = Control();
        var data = Data();

        var contents = Observable.Concat(debEntry.Bytes, control.Bytes, data.Bytes);
        await contents.DumpTo(stream);
    }

    private Entry Control()
    {
        throw new NotImplementedException();

        //var properties = new Properties
        //{
        //    FileMode   = FileMode.Parse("644"),
        //    GroupId = 0,
        //    OwnerId = 0,
        //    LastModification = DateTimeOffset.Now
        //};

        //var contents = $"""
        //                Package: {metadata.PackageName.ToLower()}
        //                Priority: optional
        //                Section: utils
        //                Maintainer: {metadata.Maintainer}
        //                Version: 2.0.4
        //                Homepage: {metadata.Homepage}
        //                Vcs-Git: git://github.com/zkSNACKs/WalletWasabi.git
        //                Vcs-Browser: https://github.com/zkSNACKs/WalletWasabi
        //                Architecture: {metadata.Architecture}
        //                License: {metadata.License}
        //                Installed-Size: 207238
        //                Recommends: policykit-1
        //                Description: open-source, non-custodial, privacy focused Bitcoin wallet
        //                  Built-in Tor, coinjoin, payjoin and coin control features.

        //                """.FromCrLfToLf();

        //return new Entry(new EntryData("control.tar", properties, () => new MemoryStream(contents.GetAsciiBytes())));
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
        throw new NotImplementedException();

        //var properties = new Properties
        //{
        //    FileMode   = FileMode.Parse("644"),
        //    GroupId = 0,
        //    OwnerId = 0,
        //    LastModification = DateTimeOffset.Now
        //};

        //var contents ="""
        //              2.0

        //              """.FromCrLfToLf();

        //return new Entry(new EntryData("debian-binary", properties, () => new MemoryStream(contents.GetAsciiBytes())));
    }
}