﻿using System.IO.Abstractions;
using System.Reactive.Linq;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Extensions;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using SharpCompress;
using Xunit;
using Zafiro.DataModel;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Zafiro.FileSystem.Unix;
using Zafiro.Mixins;
using static System.IO.File;
using File = System.IO.File;
using UnixFileMode = Zafiro.FileSystem.Unix.UnixFileMode;

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
        var byteProvider = tarFile.ToData();
        var bytes = byteProvider.Bytes();
        var actualString = Encoding.ASCII.GetString(bytes);
        actualString.Should().BeEquivalentTo(await ReadAllTextAsync("TestFiles\\Sample.tar"));
    }

    //[Fact]
    //public async Task Integration()
    //{
    //    var fileSystem = new FileSystem();
    //    var systemIODirectory = new SystemIODirectory(fileSystem.DirectoryInfo.New("C:\\Users\\JMN\\Desktop\\AppDir\\AvaloniaSyncer"));
    //    var allFiles = systemIODirectory.GetFilesInTree(ZafiroPath.Empty);

    //    var result = await allFiles
    //        .Map(x => x.Select(t => new FileTarEntry(t.FullPath(), t.Rooted, Misc.RegularFileProperties())))
    //        .Map(entries => new TarFile(entries.ToArray()))
    //        .Map(file => file.ToData())
    //        .Bind(async byteProvider =>
    //        {
    //            await using (var fileStream = Open("C:\\Users\\JMN\\Desktop\\mytarfile.tar", FileMode.Create))
    //            {
    //                return (await byteProvider.DumpTo(fileStream).ToList()).Combine();
    //            }
    //        });
    //    result.Should().Succeed();
    //}
}