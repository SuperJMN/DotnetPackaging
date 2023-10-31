using System.Reactive.Linq;
using Archiver;
using Archiver.Ar;
using Zafiro.IO;
using EntryData = Archiver.Ar.EntryData;
using Properties = Archiver.Ar.Properties;

namespace Archive.Tests.Ar;

public class ArFileTests
{
    [Fact]
    public async Task Write()
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
}