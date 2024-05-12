using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Xunit;
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

        var result = await DebFile2.Create().FromDirectory(directory).Configure(setup => { })
            .Build()
            .Bind(file => file.ToByteProvider().DumpTo(@"\\wsl.localhost\Ubuntu\home\jmn\Sample.deb"));
        result.Should().Succeed();
    }
}