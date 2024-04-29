using System.Reactive.Linq;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Extensions;
using System.Text;
using Xunit;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using static System.IO.File;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests;

public class TarFileTests
{
    [Fact]
    public async Task Write_Tar()
    {
        var entries = new List<TarEntry>
        {
            new FileTarEntry(new RootedFile(ZafiroPath.Empty, 
                new File("My entry", "My content")), new TarFileProperties()
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("777"),
                GroupId = 1000,
                GroupName = "group1",
                OwnerUsername = "owner1",
                OwnerId = 1000,
                LastModification = 1.January(2023),
            }),
            new FileTarEntry(new RootedFile(
                ZafiroPath.Empty,
                new File("Other entry", "Other content")),  new TarFileProperties()
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
}