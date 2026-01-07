namespace DotnetPackaging.Rpm.Tests;

[Collection("rpm-package")]
public class RpmPackageTests
{
    private readonly RpmPackageFixture fixture;

    public RpmPackageTests(RpmPackageFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Header_contains_expected_metadata()
    {
        var header = fixture.Archive.Header;

        header.GetString(RpmTestTags.Name).Should().Be("sample-app");
        header.GetString(RpmTestTags.Version).Should().Be("1.0.0");
        header.GetString(RpmTestTags.Release).Should().Be("1");
        header.GetString(RpmTestTags.Arch).Should().Be("x86_64");
        header.GetString(RpmTestTags.Os).Should().Be("linux");
        header.GetString(RpmTestTags.PayloadFormat).Should().Be("cpio");
        header.GetString(RpmTestTags.PayloadCompressor).Should().Be("gzip");

        header.GetStringArray(RpmTestTags.HeaderI18nTable).Should().ContainSingle().Which.Should().Be("C");
        header.GetStringArray(RpmTestTags.Summary).Should().ContainSingle().Which.Should().Be("Sample package for tests");
        header.GetStringArray(RpmTestTags.Description).Should().ContainSingle().Which.Should().Be("Sample package for tests");
    }

    [Fact]
    public void File_list_contains_expected_paths()
    {
        var paths = BuildFilePaths(fixture.Archive.Header);

        paths.Should().Contain("/opt/sample-app/config");
        paths.Should().Contain("/opt/sample-app/sample-app");
        paths.Should().Contain("/opt/sample-app/config/settings.json");
        paths.Should().Contain("/usr/share/applications/sample-app.desktop");
        paths.Should().Contain("/usr/bin/sample-app");
    }

    [Fact]
    public void Payload_contains_expected_files()
    {
        var entries = fixture.PayloadEntries;
        entries.Select(entry => entry.Name).Should().Contain(new[]
        {
            "opt/sample-app/sample-app",
            "opt/sample-app/config/settings.json",
            "usr/share/applications/sample-app.desktop",
            "usr/bin/sample-app"
        });

        var binary = entries.Single(entry => entry.Name == "opt/sample-app/sample-app");
        binary.Data.Should().StartWith(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' });

        var config = entries.Single(entry => entry.Name == "opt/sample-app/config/settings.json");
        Encoding.UTF8.GetString(config.Data).Should().Contain("\"demo\"");

        var launcher = entries.Single(entry => entry.Name == "usr/bin/sample-app");
        Encoding.ASCII.GetString(launcher.Data).Should().StartWith("#!/usr/bin/env sh\n");
    }

    [Fact]
    public void Executable_is_marked_executable()
    {
        var header = fixture.Archive.Header;
        var paths = BuildFilePaths(header);
        var modes = header.GetInt16Array(RpmTestTags.FileModes);
        var index = Array.IndexOf(paths, "/opt/sample-app/sample-app");
        index.Should().BeGreaterThanOrEqualTo(0);

        var mode = modes[index];
        (mode & 0xF000).Should().Be(0x8000);
        (mode & 0x01FF).Should().Be(0x1ED);
    }

    private static string[] BuildFilePaths(RpmHeader header)
    {
        var baseNames = header.GetStringArray(RpmTestTags.BaseNames);
        var dirNames = header.GetStringArray(RpmTestTags.DirNames);
        var dirIndexes = header.GetInt32Array(RpmTestTags.DirIndexes);
        var paths = new string[baseNames.Length];

        for (var i = 0; i < baseNames.Length; i++)
        {
            paths[i] = $"{dirNames[dirIndexes[i]]}{baseNames[i]}";
        }

        return paths;
    }
}
