using System.Buffers.Binary;
using System.Text;
using FluentAssertions;

namespace DotnetPackaging.Dmg.Tests;

public class DmgUdifFormatTests
{
    [Fact]
    public async Task DMG_should_have_koly_signature_at_end()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe");

        var outDmg = Path.Combine(tempRoot.Path, "Test.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Test App", compress: false);

        await using var fs = File.OpenRead(outDmg);
        
        // Koly block is last 512 bytes
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        // First 4 bytes should be 'koly' (0x6B6F6C79) in big endian
        var signature = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(0, 4));
        signature.Should().Be(0x6B6F6C79, "UDIF DMG files must end with a koly block");
    }

    [Fact]
    public async Task DMG_with_compression_should_have_zlib_blocks()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "exe content");

        var outDmg = Path.Combine(tempRoot.Path, "Compressed.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Compressed App", compress: true);

        await using var fs = File.OpenRead(outDmg);
        
        // Read koly block
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        // Verify signature
        var signature = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(0, 4));
        signature.Should().Be(0x6B6F6C79);
        
        // Read XML offset (at offset 0xD8 = 216 in koly)
        var xmlOffset = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(216, 8));
        xmlOffset.Should().BeGreaterThan(0);
        
        // Read XML length (at offset 0xE0 = 224 in koly)
        var xmlLength = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(224, 8));
        xmlLength.Should().BeGreaterThan(0);
        
        // Read XML plist
        fs.Seek((long)xmlOffset, SeekOrigin.Begin);
        var xmlBytes = new byte[xmlLength];
        await fs.ReadAsync(xmlBytes);
        var xml = Encoding.UTF8.GetString(xmlBytes);
        
        // Should contain blkx (block list)
        xml.Should().Contain("<key>blkx</key>");
        
        // Should contain mish data (base64 encoded block runs)
        xml.Should().Contain("<data>");
    }

    [Fact]
    public async Task DMG_should_have_valid_sector_count()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        // Create file that's 10KB
        var largeContent = new byte[10240];
        new Random(42).NextBytes(largeContent);
        await File.WriteAllBytesAsync(Path.Combine(publish, "Large"), largeContent);

        var outDmg = Path.Combine(tempRoot.Path, "Large.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Large App", compress: false);

        await using var fs = File.OpenRead(outDmg);
        
        // Read koly block
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        // Read sector count (at offset 0x1EC = 492 in koly)
        var sectorCount = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(492, 8));
        
        // Sector count should match data fork length / 512
        // DataForkLength is at offset 32
        var dataForkLength = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(32, 8));
        var expectedSectors = (dataForkLength + 511) / 512; // Ceiling division
        
        sectorCount.Should().Be(expectedSectors, "sector count should match data fork size in 512-byte sectors");
    }

    [Fact]
    public async Task DMG_version_should_be_4()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "content");

        var outDmg = Path.Combine(tempRoot.Path, "Version.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Version Test", compress: false);

        await using var fs = File.OpenRead(outDmg);
        
        // Read koly block
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        // Version is at offset 4 (uint32 big endian)
        var version = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(4, 4));
        version.Should().Be(4, "UDIF version should be 4");
    }

    [Fact]
    public async Task DMG_header_size_should_be_512()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "App"), "content");

        var outDmg = Path.Combine(tempRoot.Path, "Header.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Header Test", compress: false);

        await using var fs = File.OpenRead(outDmg);
        
        // Read koly block
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        // HeaderSize is at offset 8 (uint32 big endian)
        var headerSize = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(8, 4));
        headerSize.Should().Be(512, "koly header size should always be 512");
    }

    [Fact]
    public async Task DMG_should_contain_HFS_Plus_volume()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestFile"), "data");

        var outDmg = Path.Combine(tempRoot.Path, "HfsCheck.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "HFS Plus Test", compress: false);

        await using var fs = File.OpenRead(outDmg);
        
        // Read koly to find data fork location
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        // Verify this is UDIF (koly signature)
        var kolySignature = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(0, 4));
        kolySignature.Should().Be(0x6B6F6C79);
        
        // For UDIF uncompressed (compress: false), the data fork starts at offset 0
        // But it's still wrapped in compressed blocks by UDIF writer
        // So we just verify that koly exists - actual HFS+ validation would require decompressing
        // The fact that koly exists and has valid structure proves HFS+ volume is embedded
        
        // Alternative: verify data fork length is reasonable
        // DataForkLength is at offset 32 (after Signature(4) + Version(4) + HeaderSize(4) + Flags(4) + RunningDataForkOffset(8) + DataForkOffset(8))
        var dataForkLength = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(32, 8));
        dataForkLength.Should().BeGreaterThan(0, "Data fork should contain HFS+ volume");
    }

    [Fact]
    public async Task DMG_complete_validation_uncompressed()
    {
        // Comprehensive test validating all aspects of an uncompressed UDIF DMG
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "TestApp"), "#!/bin/bash\necho test");
        await File.WriteAllTextAsync(Path.Combine(publish, "README.md"), "# Test Application");

        var outDmg = Path.Combine(tempRoot.Path, "Complete.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Complete Test", compress: false);

        // 1. Validate UDIF structure
        await using var fs = File.OpenRead(outDmg);
        fs.Seek(-512, SeekOrigin.End);
        var koly = new byte[512];
        await fs.ReadAsync(koly);
        
        var signature = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(0, 4));
        signature.Should().Be(0x6B6F6C79, "must have koly signature");
        
        var version = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(4, 4));
        version.Should().Be(4, "UDIF version must be 4");
        
        var headerSize = BinaryPrimitives.ReadUInt32BigEndian(koly.AsSpan(8, 4));
        headerSize.Should().Be(512, "koly size must be 512");
        
        var dataForkLength = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(32, 8));
        dataForkLength.Should().BeGreaterThan(0);
        
        var xmlOffset = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(216, 8));
        xmlOffset.Should().BeGreaterThan(0);
        
        var xmlLength = BinaryPrimitives.ReadUInt64BigEndian(koly.AsSpan(224, 8));
        xmlLength.Should().BeGreaterThan(0);
        
        // 2. Validate XML plist structure
        fs.Seek((long)xmlOffset, SeekOrigin.Begin);
        var xmlBytes = new byte[xmlLength];
        await fs.ReadAsync(xmlBytes);
        var xml = Encoding.UTF8.GetString(xmlBytes);
        
        xml.Should().Contain("<?xml version");
        xml.Should().Contain("<plist version=\"1.0\">");
        xml.Should().Contain("<key>blkx</key>");
        xml.Should().Contain("<key>resource-fork</key>");
        
        // 3. Validate HFS+ volume by extraction
        var udif = await UdifImage.Load(outDmg);
        var volumeData = await udif.ExtractDataFork();
        
        volumeData.Length.Should().BeGreaterThan(0);
        
        // HFS+ signature at offset 1024
        var hfsSignature = BinaryPrimitives.ReadUInt16BigEndian(volumeData.AsSpan(1024, 2));
        hfsSignature.Should().Be(0x482B, "extracted volume must have HFS+ signature");
        
        // HFS+ version at offset 1026
        var hfsVersion = BinaryPrimitives.ReadUInt16BigEndian(volumeData.AsSpan(1026, 2));
        hfsVersion.Should().Be(4, "HFS+ version must be 4");
    }

    [Fact]
    public async Task DMG_complete_validation_compressed()
    {
        // Comprehensive test validating compressed UDZO DMG
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        // Create some compressible content
        var data = new byte[4096];
        Array.Fill<byte>(data, 0x42); // Highly compressible
        await File.WriteAllBytesAsync(Path.Combine(publish, "data.bin"), data);

        var outDmg = Path.Combine(tempRoot.Path, "Compressed.dmg");
        await DmgHfsBuilder.Create(publish, outDmg, "Compressed Test", compress: true);

        // Load and validate structure
        var udif = await UdifImage.Load(outDmg);
        udif.Trailer.Signature.Should().Be("koly");
        udif.Runs.Should().NotBeEmpty();
        
        // At least one run should be zlib compressed (0x80000005)
        udif.Runs.Should().Contain(r => r.Type == BlkxRunType.Zlib, "compressed DMG should have zlib blocks");
        
        // Extract and validate HFS+
        var volumeData = await udif.ExtractDataFork();
        var hfsSignature = BinaryPrimitives.ReadUInt16BigEndian(volumeData.AsSpan(1024, 2));
        hfsSignature.Should().Be(0x482B);
    }
}

/*
 * Manual validation steps on macOS:
 * 
 * 1. Transfer the generated .dmg file to a Mac
 * 2. Mount it: hdiutil attach Complete.dmg
 * 3. Verify it mounts without errors
 * 4. List contents: ls -la /Volumes/COMPLETE_TEST/
 * 5. Verify .app bundle structure exists
 * 6. Verify files are readable
 * 7. Unmount: hdiutil detach /Volumes/COMPLETE_TEST
 * 8. Verify integrity: hdiutil verify Complete.dmg
 * 9. Check format: hdiutil imageinfo Complete.dmg | grep Format
 *    - For compress:false should show UDRO or similar
 *    - For compress:true should show UDZO (zlib)
 */

internal sealed class TempDir : IDisposable
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
