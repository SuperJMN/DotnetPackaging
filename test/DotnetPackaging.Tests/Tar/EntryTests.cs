﻿using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Tar;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.Tests.Tar;

public class EntryTests
{
    [Fact]
    public async Task Test_entry_data_length()
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        var byteStream = await fs.GetFile("TestFiles/Content/icon.png")
            .Bind(file => file.ToByteStream())
            .Map(by => new Entry(new EntryData("Entry", new Properties()
            {
                Length = 0,
                FileMode = FileMode.Parse("555"),
                GroupId = 1,
                OwnerId = 1,
                GroupName = "root",
                LastModification = DateTimeOffset.Now,
                LinkIndicator = 1,
                OwnerUsername = "root"
            }, () => Observable.Empty<byte>(), by)));

        byteStream.Should().Succeed().And.Subject.Value.Length.Should().Be(101376+512);
    }
}