using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging.NewTar;
using FluentAssertions;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem.Local;
using IFileSystem = Zafiro.FileSystem.IFileSystem;

namespace DotnetPackaging.Tests.Tar.New;

public class TarTests
{
    private IFileSystem fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);

    [Fact]
    public async Task Lenght_test()
    {
        var result = await ResultFactory.CombineAndMap(
            EntryFactory.Create(fs, "TestFiles/icon.png"),
            EntryFactory.Create(fs, "TestFiles/control.tar"),
            (one, another) => new TarFile(one, another));

        result.Should().Succeed().And.Subject.Value.Length.Should().Be(112640L);
    }
}