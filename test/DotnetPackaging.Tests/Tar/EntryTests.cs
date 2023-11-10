using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using DotnetPackaging.NewTar;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;
using IFileSystem = Zafiro.FileSystem.IFileSystem;

namespace DotnetPackaging.Tests.Tar;

public class EntryTests
{
    private readonly LocalFileSystem fs = new(new FileSystem(), Maybe<ILogger>.None);

    [Fact]
    public async Task Test_entry_data_length()
    {
        (await Entry(fs, "TestFiles/icon.png")).Should().Succeed().And.Subject.Value.Length.Should().Be(101376+512);
    }

    private static Task<Result<Entry>> Entry(IFileSystem fs, ZafiroPath path)
    {
        return fs.GetFile(path)
            .Bind(file => file.ToByteStream())
            .Map(byteFlow => new Entry("Entry", new Properties()
            {
                Length = 0,
                FileMode = FileMode.Parse("555"),
                GroupId = 1,
                OwnerId = 1,
                GroupName = "root",
                LastModification = DateTimeOffset.Now,
                LinkIndicator = 1,
                OwnerUsername = "root"
            }, byteFlow));
    }
}