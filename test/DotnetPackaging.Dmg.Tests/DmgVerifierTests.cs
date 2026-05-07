using DotnetPackaging.Dmg.Verification;
using FluentAssertions;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgVerifierTests
{
    [Fact]
    public async Task Verify_udif_dmg_reports_udif_file_count_validation()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App");

        var result = await DmgVerifier.Verify(outDmg);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("UDIF DMG OK");
        result.Value.Should().Contain("files=");
    }
}
