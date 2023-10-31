using Archiver;
using Archiver.Ar;
using Archiver.Tar;
using EntryData = Archiver.Ar.EntryData;
using Properties = Archiver.Ar.Properties;

namespace Archive.Tests.Ar;

public class ArFileTests
{
    [Fact]
    public async Task Write()
    {
        ArFile ar;
        await using (var output = File.OpenWrite("C:\\Users\\JMN\\Desktop\\Sample.ar"))
        {
            ar = new ArFile(output);

            var properties = new Properties()
            {
                FileModes   = FileModes.Parse("777"),
                GroupId = 0,
                OwnerId = 0,
                LastModification = DateTimeOffset.Now
            };

            var contents ="""
                          2.0

                          """;

            var entryData = new EntryData("debian-binary", properties, () => new MemoryStream(contents.GetAsciiBytes()));

            await ar.Build(entryData);
        }
    }
}