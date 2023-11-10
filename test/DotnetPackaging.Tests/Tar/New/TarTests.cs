using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.New.Archives.Tar;
using FluentAssertions;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem.Local;
using IFileSystem = Zafiro.FileSystem.IFileSystem;

namespace DotnetPackaging.Tests.Tar.New;

// TODO: Place some interesting tests here
//public class TarTests
//{
//    private readonly IFileSystem fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);

//    [Fact]
//    public async Task Length_test()
//    {
//        var result = await ResultFactory.CombineAndMap(
//            EntryFactory.Create(fs, "TestFiles/icon.png", "icon.png"),
//            EntryFactory.Create(fs, "TestFiles/Hello.txt", "Hello.txt"),
//            (one, another) => new TarFile(one, another));

//        result.Should().Succeed().And.Subject.Value.Length.Should().Be(new FileInfo("TestFiles\\Sample.tar").Length);
//    }

//    [Fact]
//    public async Task Contents_test()
//    {
//        var result = await ResultFactory.CombineAndMap(
//            EntryFactory.Create(fs, "TestFiles/icon.png", "icon.png"),
//            EntryFactory.Create(fs, "TestFiles/Hello.txt", "Hello.txt"),
//            (one, another) => new TarFile(one, another));

//        var expected = await File.ReadAllBytesAsync("TestFiles\\Sample.tar");
//        result.Should().Succeed().And.Subject.Value.Bytes.ToEnumerable().ToList().Should().BeEquivalentTo(expected);
//    }
//}