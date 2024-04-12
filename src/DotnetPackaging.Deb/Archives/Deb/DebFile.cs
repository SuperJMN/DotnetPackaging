using System.Reactive.Linq;
using System.Text;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Deb.Contents;
using DotnetPackaging.Deb.Archives.Tar;
using Entry = DotnetPackaging.Deb.Archives.Tar.Entry;
using Properties = DotnetPackaging.Deb.Archives.Tar.Properties;

namespace DotnetPackaging.Deb.Archives.Deb;

public class DebFile : IByteFlow
{
    private readonly ArFile arFile;
    private readonly ContentCollection contentCollection;
    private readonly Metadata metadata;

    public DebFile(Metadata metadata, ContentCollection contentCollection)
    {
        this.metadata = metadata;
        this.contentCollection = contentCollection;
        arFile = new ArFile(DebEntry(), Control(), Data());
    }

    public IObservable<byte> Bytes => arFile.Bytes;

    public long Length => arFile.Length;

    private static Entry DirEntry(string path) => new(path, new Properties
    {
        GroupName = "root",
        OwnerUsername = "root",
        GroupId = 1000,
        OwnerId = 1000,
        FileMode = LinuxFileMode.Parse("644"),
        LastModification = DateTimeOffset.Now,
        LinkIndicator = 5
    }, new ByteFlow(Observable.Empty<byte>(), 0));

    private static Ar.Entry DebEntry()
    {
        var data = "debian-binary";

        var contents = """
                       2.0

                       """.FromCrLfToLf();

        var properties = new Ar.Properties
        {
            FileMode = LinuxFileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now
        };

        return new Ar.Entry(data, properties, contents.ToByteFlow(Encoding.ASCII));
    }

    private Ar.Entry Control()
    {
        var tarFile = ControlTar();

        var arProperties = new Ar.Properties
        {
            FileMode = LinuxFileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now
        };

        return new Ar.Entry("control.tar", arProperties, tarFile);
    }

    private TarFile ControlTar()
    {
        var data = $"""
                    Package: {metadata.PackageName}
                    Priority: optional
                    Section: utils
                    Maintainer: {metadata.Maintainer}
                    Version: {metadata.Version}
                    Homepage: {metadata.Homepage}
                    Vcs-Git: git://github.com/zkSNACKs/WalletWasabi.git
                    Vcs-Browser: https://github.com/zkSNACKs/WalletWasabi
                    Architecture: {metadata.Architecture}
                    License: {metadata.License}
                    Installed-Size: 207238
                    Recommends: policykit-1
                    Description: {metadata.Description}

                    """.FromCrLfToLf();

        var tarProperties = new Properties
        {
            FileMode = LinuxFileMode.Parse("644"),
            GroupId = 1000,
            OwnerId = 1000,
            LastModification = DateTimeOffset.Now,
            GroupName = "root",
            OwnerUsername = "root",
            LinkIndicator = 0
        };

        var dir = DirEntry("./");
        return new TarFile(dir, new Entry("./control", tarProperties, data.ToByteFlow(Encoding.ASCII)));
    }

    private Ar.Entry Data()
    {
        var dataTar = new DataFactory(metadata, contentCollection).Tar;

        var properties = new Ar.Properties
        {
            FileMode = LinuxFileMode.Parse("644"),
            GroupId = 1000,
            OwnerId = 1000,
            LastModification = DateTimeOffset.Now
        };

        return new Ar.Entry("data.tar", properties, dataTar);
    }
}