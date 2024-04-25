using FluentAssertions;
using Xunit;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Tests;

public class HelperTests
{
    [Fact]
    public void Get_directories()
    {
        var paths = new ZafiroPath[]
        {
            "One/file1.txt",
            "Two/file2.txt",
            "Three/Four/file3.txt",
        }.DirectoryPaths();

        paths.Should().BeEquivalentTo(new ZafiroPath[]{ ZafiroPath.Empty, "One", "Two", "Three/Four", "Three"});
    }
    
    [Fact]
    public void Get_directories2()
    {
        var paths = new ZafiroPath[]
        {
            "control",
        }.DirectoryPaths();

        paths.Should().BeEquivalentTo(new ZafiroPath[]{ ZafiroPath.Empty,});
    }
}