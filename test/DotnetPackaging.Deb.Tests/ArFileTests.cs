﻿using DotnetPackaging.Deb.Archives.Ar;
using FluentAssertions.Extensions;
using UnixFileMode = Zafiro.FileSystem.Unix.UnixFileMode;

namespace DotnetPackaging.Deb.Tests;

public class ArFileTests
{
    [Fact]
    public async Task WriteAr()
    {
        var entries = new List<Entry>
        {
            new(new File("My entry", "Some content"), DefaultProperties()),
            new(new File("Some other entry", "Other content"), DefaultProperties())
        };

        var arFile = new ArFile(entries.ToArray());
        var byteProvider = arFile.ToData();
        
        var expected = """
                      !<arch>
                      My entry        1579474800  0     0     100777  12        `
                      Some contentSome other entry1579474800  0     0     100777  13        `
                      Other content
                      """.FromCrLfToLf();

        var bytes = (await byteProvider.Bytes.ToList()).SelectMany(x => x);
        var s = Encoding.ASCII.GetString(bytes.ToArray());
        s.Should().BeEquivalentTo(expected);
    }

    private static Properties DefaultProperties() => new()
    {
        FileMode = UnixFileMode.All,
        GroupId = 0,
        LastModification = 20.January(2020),
        OwnerId = 0
    };
}