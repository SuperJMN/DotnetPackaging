using FluentAssertions;

namespace Archive.Tests;

public class FileModeTests
{
    [Fact]
    public void Test()
    {
        var fileModes = FileMode.Parse("764");
        
        var str = fileModes.ToString();
        str.Should().Be("764");
    }
}