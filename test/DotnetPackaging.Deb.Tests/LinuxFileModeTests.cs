using FluentAssertions;

namespace DotnetPackaging.Tests;

public class LinuxFileModeTests
{
    [Fact]
    public void Test()
    {
        var fileModes = LinuxFileMode.Parse("764");
        
        var str = fileModes.ToString();
        str.Should().Be("764");
    }
}