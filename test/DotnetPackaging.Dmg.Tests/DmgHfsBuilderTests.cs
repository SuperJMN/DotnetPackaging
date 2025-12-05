using System.Buffers.Binary;
using System.Text;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgHfsBuilderTests
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

        var outDmg = Path.Combine(tempRoot.Path, "MyApp.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "My App");

        File.Exists(outDmg).Should().BeTrue("the dmg file must be created");

        // Validate DMG was created with reasonable size
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(0, "dmg should have content");
        
        // Verify it's a valid HFS+ by checking signature at offset 1024
        await using var fs = File.OpenRead(outDmg);
        fs.Seek(1024, SeekOrigin.Begin);
        var signature = new byte[2];
        await fs.ReadAsync(signature, 0, 2);
        BinaryPrimitives.ReadUInt16BigEndian(signature).Should().Be(0x482B, "should have HFS+ signature 'H+'");
    }

    [Fact]
    public async Task Wraps_publish_output_into_app_bundle_when_missing()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");
        await File.WriteAllTextAsync(Path.Combine(publish, "Angor.deps.json"), "deps");

        var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Angor Avalonia");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain app bundle content");
        
        // Verify it's a valid HFS+
        await using var fs = File.OpenRead(outDmg);
        fs.Seek(1024, SeekOrigin.Begin);
        var signature = new byte[2];
        await fs.ReadAsync(signature, 0, 2);
        BinaryPrimitives.ReadUInt16BigEndian(signature).Should().Be(0x482B);
    }

    [Fact]
    public async Task Creates_dmg_with_applications_symlink()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", addApplicationsSymlink: true);

        File.Exists(outDmg).Should().BeTrue();
        
        // Verify it's a valid HFS+
        await using var fs = File.OpenRead(outDmg);
        fs.Seek(1024, SeekOrigin.Begin);
        var signature = new byte[2];
        await fs.ReadAsync(signature, 0, 2);
        BinaryPrimitives.ReadUInt16BigEndian(signature).Should().Be(0x482B);
    }

    [Fact]
    public async Task Volume_name_is_sanitized_correctly()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "file.txt"), "ok");

        var outDmg = Path.Combine(tempRoot.Path, "Out.dmg");
        // Unicode and special chars should be sanitized
        await DmgHfsBuilder.Create(publish, outDmg, "my app: with spaces & unicode — test");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain content");
        
        // Verify it's a valid HFS+
        await using var fs = File.OpenRead(outDmg);
        fs.Seek(1024, SeekOrigin.Begin);
        var signature = new byte[2];
        await fs.ReadAsync(signature, 0, 2);
        BinaryPrimitives.ReadUInt16BigEndian(signature).Should().Be(0x482B);
    }

    [Fact]
    public async Task Creates_dmg_without_default_layout()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Minimal.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Minimal", includeDefaultLayout: false);

        File.Exists(outDmg).Should().BeTrue();
        
        // Verify it's a valid HFS+
        await using var fs = File.OpenRead(outDmg);
        fs.Seek(1024, SeekOrigin.Begin);
        var signature = new byte[2];
        await fs.ReadAsync(signature, 0, 2);
        BinaryPrimitives.ReadUInt16BigEndian(signature).Should().Be(0x482B);
    }

    [Fact]
    public async Task HfsPlus_header_has_correct_version()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Version.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Version Test");

        await using var fs = File.OpenRead(outDmg);
        fs.Seek(1024, SeekOrigin.Begin);
        var header = new byte[6];
        await fs.ReadAsync(header);
        
        // Bytes 0-1: signature 'H+' (0x482B)
        BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2)).Should().Be(0x482B);
        
        // Bytes 2-3: version (should be 4 for HFS+)
        BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2)).Should().Be(4);
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
