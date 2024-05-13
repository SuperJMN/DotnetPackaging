namespace DotnetPackaging.Deb.Tests;

public class StringManipulationTests
{
    [Theory]
    [InlineData("Hola", 3, "Hol")]
    [InlineData("Hola", 4, "Hola")]
    [InlineData("Hola", 5, "Hola")]
    public void Truncate(string str, int length, string expected)
    {
        str.Truncate(length).Should().Be(expected);
    }

    [Theory]
    [InlineData("664", 8, "0000664\0")]
    public void NullTerminatedPaddedField(string str, int length, string expected)
    {
        str.NullTerminatedPaddedField(length).Should().Be(expected);
    } 
}