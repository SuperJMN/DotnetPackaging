﻿using Archiver.Deb;
using FluentAssertions;
using Zafiro.FileSystem;

namespace Archive.Tests.Deb;

public class PathGeneratorTests
{
    [Fact]
    public void Directories()
    {
        var sut = new DebPaths("SamplePackage", new[]
        {
            new ZafiroPath("Subdir/File1.txt"),
            new ZafiroPath("Subdir/File2.txt")
        });

        var dirs = sut.Directories().Select(x => x.Path);
        dirs.Should().BeEquivalentTo(new []
        {
            ".",
            "./usr",
            "./usr/local",
            "./usr/local/bin",
            "./usr/local/bin/SamplePackage",
            "./usr/local/bin/SamplePackage/Subdir",
        });
    }
}