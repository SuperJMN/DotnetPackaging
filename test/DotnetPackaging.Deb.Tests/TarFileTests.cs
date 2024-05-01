using System.IO.Abstractions;
using System.Reactive.Linq;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Extensions;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using SharpCompress;
using Xunit;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using static System.IO.File;
using File = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class TarFileTests
{
    [Fact]
    public async Task Write_Tar()
    {
        var entries = new List<TarEntry>
        {
            new FileTarEntry("My entry", new StringByteProvider("My content", Encoding.ASCII), new TarFileProperties()
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("777"),
                GroupId = 1000,
                GroupName = "group1",
                OwnerUsername = "owner1",
                OwnerId = 1000,
                LastModification = 1.January(2023),
            }),
            new FileTarEntry("Other entry", new StringByteProvider("Other content", Encoding.ASCII),  new TarFileProperties()
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("755"),
                GroupId = 123,
                OwnerId = 567,
                GroupName = "group2",
                OwnerUsername = "owner2",
                LastModification = 1.January(2023),
            })
        };

        var tarFile = new TarFile(entries.ToArray());
        var byteProvider = tarFile.ToByteProvider();

        var bytes = (await byteProvider.Bytes.ToList()).SelectMany(x => x).ToArray();
        await WriteAllBytesAsync("C:\\Users\\JMN\\Desktop\\mytar.tar", bytes);
        var actualString = Encoding.ASCII.GetString(bytes);
        actualString.Should().BeEquivalentTo(await ReadAllTextAsync("TestFiles\\Sample.tar"));
    }

    [Fact]
    public async Task Integration()
    {
        var fileSystem = new FileSystem();
        var systemIODirectory = new SystemIODirectory(fileSystem.DirectoryInfo.New("C:\\Users\\JMN\\Desktop\\Testing"));
        var allFiles = systemIODirectory.GetFilesInTree(ZafiroPath.Empty);
        var contents = new ByteArrayByteProvider(await ReadAllBytesAsync(@"C:\Users\JMN\Desktop\Testing\AvaloniaSyncer.Desktop.runtimeconfig.json"));
        var contents2 = allFiles.Result.Value.First();
        var fileBytes = contents2.Rooted.Bytes.Flatten().ToEnumerable().ToList();
        var tarEntry = new FileTarEntry("AvaloniaSyncer.Desktop.runtimeconfig.json", contents2, Misc.RegularFileProperties());
        var tarEntryBytes = tarEntry.ToByteProvider().Bytes.Flatten().ToEnumerable().ToList();
        await tarEntryBytes.DumpTo("C:\\Users\\JMN\\Desktop\\Entry.bin");
        var tarFile = new TarFile(tarEntry);
        await using (var fileStream = File.Open("C:\\Users\\JMN\\Desktop\\mytarfile.tar", FileMode.Create))
        {
            var byteProvider = tarFile.ToByteProvider();
            var tarfilebytes = byteProvider.Bytes.Flatten().ToEnumerable().ToList();
            var result = (await byteProvider.DumpTo(fileStream).ToList()).Combine();
        }
    }
}