using DotnetPackaging.Tar;
using FluentAssertions;

namespace DotnetPackaging.Tests.Extra;

public class MathTests
{
    [Theory]
    [InlineData(512, 512)]
    [InlineData(513, 1024)]
    [InlineData(511, 512)]
    [InlineData(0, 0)]
    public void RoundUpToNearestMultiple(int number, int expected)
    {
        number.RoundUpToNearestMultiple(512).Should().Be(expected);
    }
}