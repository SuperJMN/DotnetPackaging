using Archiver;
using Archiver.Ar;
using EntryData = Archiver.Ar.EntryData;
using Properties = Archiver.Ar.Properties;

namespace Archive.Tests.Ar;

public class ArFileTests
{
    [Fact]
    public async Task Write()
    {
        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Actual.ar");
        var ar = new ArFile(output);

        var properties = new Properties()
        {
            FileMode   = FileMode.Parse("644"),
            GroupId = 0,
            OwnerId = 0,
            LastModification = DateTimeOffset.Now
        };

        var contents ="""
                      2.0

                      """.FromCrLfToLf();

        var entry1 = new EntryData("debian-binary", properties, () => new MemoryStream(contents.GetAsciiBytes()));
        var entry2 = new EntryData("Archive1.txt", properties, () => new MemoryStream("Hola".GetAsciiBytes()));
        var entry3 = new EntryData("Archive1.txt", properties, () => new MemoryStream("Salud y buenos alimentos".GetAsciiBytes()));

        await ar.Build(entry1, entry2, entry3);
    }
}