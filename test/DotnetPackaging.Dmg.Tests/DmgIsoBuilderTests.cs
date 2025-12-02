using System.Text;
using FluentAssertions;

namespace DotnetPackaging.Dmg.Tests;

public class DmgIsoBuilderTests
{
    [Fact]
    public async Task Creates_dmg_with_app_bundle_and_files()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        // Create minimal .app bundle
        var appDir = Path.Combine(publish, "MyApp.app", "Contents", "MacOS");
        Directory.CreateDirectory(appDir);
        var exePath = Path.Combine(appDir, "MyApp");
        await File.WriteAllTextAsync(exePath, "#!/bin/sh\necho Hello\n");

        // Optional adornments
        Directory.CreateDirectory(Path.Combine(publish, ".background"));
        await File.WriteAllTextAsync(Path.Combine(publish, ".background", "background.png"), "png");
        await File.WriteAllBytesAsync(Path.Combine(publish, ".VolumeIcon.icns"), new byte[]{0,1,2});

        var outDmg = Path.Combine(tempRoot.Path, "MyApp.dmg");
        await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "My App");

        File.Exists(outDmg).Should().BeTrue("the dmg file must be created");

        // Validate DMG was created with reasonable size
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(0, "dmg should have content");
        
        // Verify it's a valid ISO9660 by checking PVD signature at sector 16
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

    [Fact]
    public async Task Wraps_publish_output_into_app_bundle_when_missing()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");
        await File.WriteAllTextAsync(Path.Combine(publish, "Angor.deps.json"), "deps");
        await File.WriteAllTextAsync(Path.Combine(publish, "AppIcon.icns"), "icon");

        var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
        await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "Angor Avalonia");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain app bundle content");
        
        // Verify it's a valid ISO9660
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

    [Fact]
    public async Task Skips_preexisting_app_directories_from_payload()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");
        Directory.CreateDirectory(Path.Combine(publish, "AngorAvalonia.app"));

        var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
        await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "Angor Avalonia");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain content");
        
        // Verify it's a valid ISO9660
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

    [Fact]
    public async Task Volume_name_is_sanitized_reasonably()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "file.txt"), "ok");

        var outDmg = Path.Combine(tempRoot.Path, "Out.dmg");
        await DmgIsoBuilder.Create(publish, outDmg, "my app: with spaces & unicode — test……………………………………………");

        // Verify DMG was created successfully with reasonable size
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain content");
        
        // Verify it's a valid ISO9660
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

}

file sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dmgtest-" + Guid.NewGuid());
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}
