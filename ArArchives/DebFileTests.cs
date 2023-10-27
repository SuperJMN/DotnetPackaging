using System.IO.Abstractions;
using Archiver;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem.Local;

namespace Archive.Tests;

public class DebFileTests
{
    [Fact]
    public async Task Create()
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        var result =await fs
            .GetDirectory("D:/Binarios/Portables/AvaloniaSynchronizer")
            .Bind(directory =>
            {
                var metadata = new Metadata("AvaloniaSyncer", "AvaloniaSyncer", "SuperJMN")
                {
                    Architecture = "amd64",
                    Homepage = "http://www.superjmn.com",
                    License = "MIT",
                };

                return DebFile.Create(File.Create("c:\\users\\jmn\\Desktop\\file.deb"), directory, metadata);
            });
        result.Should().Succeed();
    }
}