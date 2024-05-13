using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Tests.Integration;

public class DebTests
{
    [Fact]
    public async Task Integration()
    {
        var fileSystem = new FileSystem();
        var directory = new DotnetDir(fileSystem.DirectoryInfo.New(@"C:\Users\JMN\Desktop\AppDir\AvaloniaSyncer"));

        var result = await DebFile.From()
            .Directory(directory)
            .Configure(setup => setup.WithComment("Hi"))
            .Build()
            .Bind(file => file.ToData().DumpTo(@"\\wsl.localhost\Ubuntu\home\jmn\Sample.deb"));

        result.Should().Succeed();
    }
}