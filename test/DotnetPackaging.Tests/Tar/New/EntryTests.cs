using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.Tests.Tar.New;

public class EntryTests
{
    private readonly LocalFileSystem fs = new(new FileSystem(), Maybe<ILogger>.None);

    [Fact]
    public async Task Test_entry_data_length()
    {
        (await EntryFactory.Create(fs, "TestFiles/icon.png", "Entry")).Should().Succeed().And.Subject.Value.Length.Should().Be(101376 + 512);
    }
}