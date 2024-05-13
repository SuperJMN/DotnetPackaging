using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions.Extensions;
using Zafiro.DataModel;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Unix;
using Zafiro.Mixins;
using Zafiro.Reactive;
using static System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class TarFileTests
{
    [Fact]
    public async Task Write_Tar()
    {
        var entries = new List<TarEntry>
        {
            new FileTarEntry("My entry", new StringData("My content", Encoding.ASCII), new TarFileProperties()
            {
                FileMode = "777".ToFileMode(),
                GroupId = 1000,
                GroupName = "group1",
                OwnerUsername = "owner1",
                OwnerId = 1000,
                LastModification = 1.January(2023),
            }),
            new FileTarEntry("Other entry", new StringData("Other content", Encoding.ASCII), new TarFileProperties()
            {
                FileMode = "755".ToFileMode(),
                GroupId = 123,
                OwnerId = 567,
                GroupName = "group2",
                OwnerUsername = "owner2",
                LastModification = 1.January(2023),
            })
        };

        var tarFile = new TarFile(entries.ToArray());
        var data = tarFile.ToData();
        await data.DumpTo("C:\\Users\\JMN\\Desktop\\actual.tar");
        var compare = await IsTarDataEquals(data, new ByteArrayData(await ReadAllBytesAsync("TestFiles\\Sample.tar")));
        compare.Should().BeTrue();
    }

    private static async Task<bool> IsTarDataEquals(IData first, IData second)
    {
        var shorter = first.Length > second.Length ? second : first;
        var dataUntilEof = await shorter.Bytes.Flatten()
            .Buffer(1024)
            .Select(x => x.Any(y => !Equals(y, default(byte))) ? x : Enumerable.Empty<byte>())
            .TakeWhile(x => x.Any()).ToList();

        var eof = dataUntilEof.Flatten();
        var eofPosition = eof.Count();
        
        return first.Bytes().Take(eofPosition).SequenceEqual(second.Bytes().Take(eofPosition));
    }
}