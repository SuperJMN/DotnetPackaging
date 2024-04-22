using DotnetPackaging.Deb.Archives;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.Mixins;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests.Ar;

public class ArFileTests
{
    [Fact]
    public async Task WriteAr()
    {
        var entries = new List<Entry>()
        {
            new(new File("My entry", Mixin.SuccessFunc("Some content".ToStream())), DefaultProperties()),
            new(new File("Some other entry", Mixin.SuccessFunc("Other content".ToStream())), DefaultProperties())
        };
        
        var arFile = new ArFile(entries.ToArray());
        var outputStream = new MemoryStream();
        var result = await ArWriter.Write(arFile, outputStream);
        result.Should().Succeed();
        var content = """
                      <!arch>
                      1010101
                      My entry
                      Some content
                      Some other entry
                      Other content
                      """.FromCrLfToLf();

        outputStream.ToArray().ToAscii().Should().Be(content);
    }

    private static Properties DefaultProperties() => new()
    {
        FileMode = UnixFilePermissions.AllPermissions,
        GroupId = 0,
        LastModification = 20.January(2020),
        OwnerId = 0
    };
    //[Fact]
    //public async Task Regular_file()
    //{
    //    var contents = """
    //                  2.0

    //                  """.FromCrLfToLf();

    //    var properties = new New.Archives.Ar.Properties()
    //    {
    //        FileMode = FileMode.Parse("644"),
    //        GroupId = 0,
    //        OwnerId = 0,
    //        LastModification = DateTimeOffset.Now,
    //    };

    //    var entry1 = New.Archives.Ar.Entry("debian-binary", properties, contents.ToByteFlow(Encoding.ASCII));
    //    var entry2 = new Entry("Archive1.txt", properties, "Hola".ToByteFlow(Encoding.ASCII));
    //    var entry3 = new Entry("Archive1.txt", properties, "Salud y buenos alimentos".ToByteFlow(Encoding.ASCII));

    //    var ar = new ArFile(entry1, entry2, entry3);

    //    await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Actual.ar");
    //    await ar.Bytes.DumpTo(output);
    //}

    //[Fact]
    //public async Task Ar_with_tar_inside()
    //{
    //    var tarFile = TarFile();

    //    var properties = new Properties()
    //    {
    //        FileMode = FileMode.Parse("644"),
    //        GroupId = 0,
    //        OwnerId = 0,
    //        LastModification = DateTimeOffset.Now,
    //    };

    //    var entry = new Entry("File.tar", properties, TarFile());
    //    var entry2 = new Entry("Greetings.txt", new Properties()
    //    {
    //        FileMode = FileMode.Parse("644"),
    //        GroupId = 0,
    //        OwnerId = 0,
    //        LastModification = DateTimeOffset.Now,
    //    }, "Saludos cordiales".ToByteFlow(Encoding.ASCII));
    //    await using var output = File.Create("C:\\Users\\JMN\\Desktop\\ArWithTarInside.ar");
    //    await new ArFile(entry, entry2).Bytes.DumpTo(output);
    //}

    //[Fact]
    //public async Task DebFromFiles()
    //{
    //    var deb = GetDeb();
    //    var control = GetControl();
    //    var data = GetData();

    //    var arFile = new ArFile(deb, control, data);
    //    await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\CraftedFromIndividualFiles.deb");
    //    await arFile.Bytes.DumpTo(output);
    //}

    //private Entry GetData()
    //{
    //    return Entry.FromStream("data.tar", () => File.OpenRead(@"C:\Users\JMN\Desktop\Testing\Deb\data.tar"));
    //}

    //private Entry GetControl()
    //{
    //    return Entry.FromStream("control.tar", () => File.OpenRead(@"C:\Users\JMN\Desktop\Testing\Deb\control.tar"));
    //}

    //private Entry GetDeb()
    //{
    //    return Entry.FromStream("debian-binary", () => File.OpenRead(@"C:\Users\JMN\Desktop\Testing\Deb\debian-binary"));
    //}

    //public TarFile TarFile()
    //{
    //    var entry1 = new Old.Tar.EntryData("recordatorioCita.pdf", new Old.Tar.Properties
    //    {
    //        FileMode = FileMode.Parse("644"),
    //        GroupId = 1000,
    //        GroupName = "jmn",
    //        LastModification = DateTimeOffset.Now,
    //        OwnerId = 1000,
    //        OwnerUsername = "jmn",
    //        Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf").Length,
    //        LinkIndicator = 0,
    //    }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf"), stream => stream.ToObservable()));

    //    var entry2 = new Old.Tar.EntryData("wasabi.deb", new Old.Tar.Properties
    //    {
    //        FileMode = FileMode.Parse("644"),
    //        GroupId = 1000,
    //        GroupName = "jmn",
    //        LastModification = DateTimeOffset.Now,
    //        OwnerId = 1000,
    //        OwnerUsername = "jmn",
    //        Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb").Length,
    //        LinkIndicator = 0,
    //    }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb"), stream => stream.ToObservable()));

    //    return new TarFile(entry1, entry2);
    //}
}