using Archiver.Tar;
using FluentAssertions;

namespace Archive.Tests;

public class FileModesTests
{
    [Fact]
    public void Test()
    {
        var fileModes = FileModes.Parse("764");
        
        var str = fileModes.ToString();
        str.Should().Be("764");
    }
}