﻿using System.Reactive.Linq;
using DotnetPackaging.Deb.Archives.Deb;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Reactive;
using static DotnetPackaging.Deb.Tests.TestMixin;
using File = Zafiro.FileSystem.Lightweight.File;
using IoFile = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class DebTests
{
    [Fact]
    public async Task Deb_test()
    {
        var dateTimeOffset = 24.April(2024).AddHours(14).AddMinutes(11).AddSeconds(5).ToDateTimeOffset();
        
        var deb = new DebFile(new ControlMetadata
            {
                Package = "Test",
                Version = "1.0-1",
                Section = "Utils",
                Priority = "optional",
                Architecture = "All",
                Maintainer = "Baeldung <test@test.com>",
                Description = "This is a test application\n for packaging",
            },
            new FileEntry(
                new RootedFile("etc", new File("etc", String("NAME=Test"))), new UnixFileProperties()
                {
                    FileMode = UnixFilePermissions.Read | UnixFilePermissions.Write | UnixFilePermissions.GroupRead | UnixFilePermissions.OtherRead,
                    GroupId = 0,
                    OwnerUsername = "jmn",
                    GroupName = "jmn",
                    LinkIndicator = 1,
                    OwnerId = 0,
                    LastModification = dateTimeOffset
                }));

        var memoryStream = new MemoryStream();

        var result = await DebFileWriter.Write(deb, memoryStream);
        result.Should().Succeed();
        await using (var fileStream = IoFile.Open("C:\\Users\\JMN\\Desktop\\Sample.deb", FileMode.Create))
        {
            memoryStream.WriteTo(fileStream);
        }

        memoryStream.Position = 0;
        
        var actual = await memoryStream.ToObservable().Take(20).ToList();
        var expected = await IoFile.OpenRead("TestFiles/Sample.deb").ToObservable().Take(20).ToList();
        actual.Should().BeEquivalentTo(expected);
    }
}