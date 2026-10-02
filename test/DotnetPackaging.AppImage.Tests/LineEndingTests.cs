using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Metadata;
using FluentAssertions;

namespace DotnetPackaging.AppImage.Tests;

public class LineEndingTests
{
    [Fact]
    public void Desktop_file_uses_line_feeds_only()
    {
        var desktopFile = new DesktopFile
        {
            Name = "App",
            Exec = "/usr/bin/App",
            Comment = Maybe.From("Comment"),
            Version = Maybe.From("1.0.0")
        };

        var contents = MetadataGenerator.DesktopFileContents(desktopFile);

        // Desktop file validators reject carriage returns
        contents.Should().NotContain("\r");
        contents.Should().EndWith("\n");
    }

    [Theory]
    [MemberData(nameof(MaintainerScripts))]
    public void Deb_maintainer_scripts_use_line_feeds_only(string script)
    {
        script.Should().StartWith("#!/bin/sh\n");
        script.Should().NotContain("\r");
    }

    public static TheoryData<string> MaintainerScripts() => new()
    {
        TextTemplates.PostInstScript("app"),
        TextTemplates.PreRmScript("app"),
        TextTemplates.PostRmScript("app")
    };
}
