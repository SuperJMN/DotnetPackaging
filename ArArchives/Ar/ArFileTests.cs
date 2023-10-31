using System.Reactive.Linq;
using Archiver;
using Archiver.Ar;
using Archiver.Tar;
using Zafiro.IO;
using EntryData = Archiver.Ar.EntryData;
using Properties = Archiver.Ar.Properties;

namespace Archive.Tests.Ar;

public class ArFileTests
{
    [Fact]
    public async Task Regular_file()
    {
        var contents ="""
                      2.0

                      """.FromCrLfToLf();

        var properties = new Properties()
        {
            FileMode   = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now,
            Length = contents.Length,
        };

        var entry1 = new EntryData("debian-binary", properties, () => contents.GetAsciiBytes().ToObservable());
        var entry2 = new EntryData("Archive1.txt", properties, () => "Hola".GetAsciiBytes().ToObservable());
        var entry3 = new EntryData("Archive1.txt", properties, () => "Salud y buenos alimentos".GetAsciiBytes().ToObservable());

        var ar = new ArFile(entry1, entry2, entry3);

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Actual.ar");
        await ar.Bytes.DumpTo(output);
    }

    [Fact]
    public async Task Ar_with_tar_inside()
    {
        var tarFile = TarFile();

        var properties = new Properties()
        {
            FileMode   = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now,
            Length = tarFile.Bytes.ToEnumerable().Count(),
        };

        var entry = new EntryData("File.tar", properties, () => TarFile().Bytes);
        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\ArWithTarInside.ar");
        await new ArFile(entry).Bytes.DumpTo(output);
    }

    public TarFile TarFile()
    {
        var entry1 = new Archiver.Tar.EntryData("recordatorioCita.pdf", new Archiver.Tar.Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf").Length
        }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf"), stream => stream.ToObservable()));

        var entry2 = new Archiver.Tar.EntryData("wasabi.deb", new Archiver.Tar.Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb").Length
        }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb"), stream => stream.ToObservable()));

        return new TarFile(entry1, entry2);
    }
}