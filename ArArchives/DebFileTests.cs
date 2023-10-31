using System.IO.Abstractions;
using Archiver.Deb;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem.Local;
using DebFile = Archiver.DebFile;

namespace Archive.Tests;

public class DebFileTests
{
    [Fact]
    public async Task Create()
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        var result = await fs
            .GetDirectory("D:/Binarios/Portables/AvaloniaSynchronizer")
            .Bind(directory =>
            {
                var metadata = new Metadata("AvaloniaSyncer", "AvaloniaSyncer", "SuperJMN")
                {
                    Architecture = "amd64",
                    Homepage = "http://www.superjmn.com",
                    License = "MIT",
                };

                using var fileStream = File.Create("c:\\users\\jmn\\Desktop\\file.deb");
                return new DebFile(metadata).Write(fileStream, directory);
            });
        result.Should().Succeed();
    }
}