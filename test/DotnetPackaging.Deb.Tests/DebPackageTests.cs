namespace DotnetPackaging.Deb.Tests;

[Collection("deb-package")]
public class DebPackageTests
{
    private readonly DebPackageFixture fixture;

    public DebPackageTests(DebPackageFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Ar_archive_contains_required_members()
    {
        fixture.ArEntries.Should().HaveCount(3);
        fixture.PackageBytes[..8].Should().Equal(Encoding.ASCII.GetBytes("!<arch>\n"));

        fixture.ArEntries.Select(e => e.Name).Should().ContainInOrder("debian-binary", "control.tar", "data.tar");

        var debianBinary = fixture.GetEntry("debian-binary");
        Encoding.ASCII.GetString(debianBinary.Data).Should().Be("2.0\n");
    }

    [Fact]
    public void Control_tar_contains_expected_metadata()
    {
        var controlEntry = fixture.GetEntry("control.tar");
        var controlTar = TarArchiveReader.ReadEntries(controlEntry.Data);
        controlTar.Select(e => e.Name).Should().Contain("control");

        var controlFile = controlTar.Single(e => e.Name == "control");
        var fields = ParseFields(Encoding.ASCII.GetString(controlFile.Data));

        fields.Should().ContainKey("Package").WhoseValue.Should().Be("sample-app");
        fields.Should().ContainKey("Version").WhoseValue.Should().Be("1.0.0");
        fields.Should().ContainKey("Architecture").WhoseValue.Should().Be("amd64");
        fields.Should().ContainKey("Description").WhoseValue.Should().Be("Sample package for tests");
        fields.Should().ContainKey("Maintainer").WhoseValue.Should().Be("Unknown Maintainer <unknown@example.com>");
    }

    [Fact]
    public void Data_tar_includes_payload_and_launchers()
    {
        var dataEntry = fixture.GetEntry("data.tar");
        var dataTar = TarArchiveReader.ReadEntries(dataEntry.Data);

        dataTar.Select(e => e.Name).Should().Contain(new[]
        {
            "opt/sample-app/sample-app",
            "opt/sample-app/config/settings.json",
            "usr/share/applications/sample-app.desktop",
            "usr/bin/sample-app"
        });

        var binary = dataTar.Single(e => e.Name == "opt/sample-app/sample-app");
        binary.Data.Should().StartWith(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' });

        var config = dataTar.Single(e => e.Name == "opt/sample-app/config/settings.json");
        Encoding.UTF8.GetString(config.Data).Should().Contain("\"demo\"");

        var launcher = dataTar.Single(e => e.Name == "usr/bin/sample-app");
        var launcherText = Encoding.ASCII.GetString(launcher.Data);
        launcherText.Should().StartWith("#!/usr/bin/env sh\n");
        launcherText.Should().Contain("/opt/sample-app/sample-app");

        var desktop = dataTar.Single(e => e.Name == "usr/share/applications/sample-app.desktop");
        var desktopText = Encoding.ASCII.GetString(desktop.Data);
        desktopText.Should().Contain("Name=Sample App");
        desktopText.Should().Contain("Exec=\"/opt/sample-app/sample-app\"");
    }

    private static IDictionary<string, string> ParseFields(string content)
    {
        return content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(": ", 2, StringSplitOptions.None))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
    }
}
