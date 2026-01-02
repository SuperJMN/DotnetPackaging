using System.Text;
using FluentAssertions;

namespace DotnetPackaging.Flatpak.Tests;

public sealed class OstreeEncodersTests
{
    [Fact]
    public void EncodeCommit_produces_valid_byte_array()
    {
        var treeChecksum = new byte[32];
        Array.Fill(treeChecksum, (byte)0xAA);

        var metaChecksum = new byte[32];
        Array.Fill(metaChecksum, (byte)0xBB);

        var result = OstreeEncoders.EncodeCommit(
            treeContentsChecksum: treeChecksum,
            dirmetaChecksum: metaChecksum,
            subject: "Test commit",
            body: "Test body",
            timestamp: DateTimeOffset.FromUnixTimeSeconds(1704067200) // 2024-01-01 00:00:00 UTC
        );

        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(64 + 11 + 9); // checksums + subject + body minimum
    }

    [Fact]
    public void EncodeCommit_with_parent_includes_parent_checksum()
    {
        var treeChecksum = new byte[32];
        var metaChecksum = new byte[32];
        var parentChecksum = new byte[32];
        Array.Fill(parentChecksum, (byte)0xCC);

        var result = OstreeEncoders.EncodeCommit(
            treeContentsChecksum: treeChecksum,
            dirmetaChecksum: metaChecksum,
            subject: "Child commit",
            body: "",
            timestamp: DateTimeOffset.UtcNow,
            parentChecksum: parentChecksum
        );

        result.Should().NotBeEmpty();
        // Parent checksum should be embedded
        result.Length.Should().BeGreaterThan(96); // tree + meta + parent
    }

    [Fact]
    public void EncodeDirTree_with_files_produces_valid_structure()
    {
        var fileChecksum = new byte[32];
        Array.Fill(fileChecksum, (byte)0x11);

        var files = new[]
        {
            ("file1.txt", fileChecksum),
            ("file2.txt", fileChecksum)
        };

        var result = OstreeEncoders.EncodeDirTree(
            files,
            Enumerable.Empty<(string, byte[], byte[])>()
        );

        result.Should().NotBeEmpty();
        // Should contain both filenames null-terminated and checksums
        var text = Encoding.UTF8.GetString(result);
        text.Should().Contain("file1.txt");
        text.Should().Contain("file2.txt");
    }

    [Fact]
    public void EncodeDirTree_with_directories_produces_valid_structure()
    {
        var contentsChecksum = new byte[32];
        Array.Fill(contentsChecksum, (byte)0x22);

        var metaChecksum = new byte[32];
        Array.Fill(metaChecksum, (byte)0x33);

        var dirs = new[]
        {
            ("subdir", contentsChecksum, metaChecksum)
        };

        var result = OstreeEncoders.EncodeDirTree(
            Enumerable.Empty<(string, byte[])>(),
            dirs
        );

        result.Should().NotBeEmpty();
        var text = Encoding.UTF8.GetString(result);
        text.Should().Contain("subdir");
    }

    [Fact]
    public void EncodeDirTree_sorts_entries_alphabetically()
    {
        var checksum = new byte[32];

        var files = new[]
        {
            ("zebra.txt", checksum),
            ("alpha.txt", checksum),
            ("middle.txt", checksum)
        };

        var result = OstreeEncoders.EncodeDirTree(files, Enumerable.Empty<(string, byte[], byte[])>());

        var text = Encoding.UTF8.GetString(result);
        var alphaIndex = text.IndexOf("alpha.txt", StringComparison.Ordinal);
        var middleIndex = text.IndexOf("middle.txt", StringComparison.Ordinal);
        var zebraIndex = text.IndexOf("zebra.txt", StringComparison.Ordinal);

        alphaIndex.Should().BeLessThan(middleIndex);
        middleIndex.Should().BeLessThan(zebraIndex);
    }

    [Fact]
    public void EncodeDirMeta_produces_correct_format()
    {
        var result = OstreeEncoders.EncodeDirMeta(
            uid: 1000,
            gid: 1000,
            mode: 0x81A4 // -rw-r--r--
        );

        // Should have 12 bytes minimum: 3 x uint32 (4 bytes each)
        result.Length.Should().BeGreaterOrEqualTo(12);
    }

    [Fact]
    public void EncodeDirMeta_with_xattrs_includes_them()
    {
        var xattrs = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var result = OstreeEncoders.EncodeDirMeta(
            uid: 0,
            gid: 0,
            mode: 0x41ED, // drwxr-xr-x
            xattrs: xattrs
        );

        result.Length.Should().BeGreaterOrEqualTo(16); // 12 bytes header + 4 bytes xattrs
    }

    [Fact]
    public void ComputeChecksum_produces_32_byte_sha256()
    {
        var data = Encoding.UTF8.GetBytes("test data");

        var result = OstreeEncoders.ComputeChecksum(data);

        result.Length.Should().Be(32);
    }

    [Fact]
    public void ComputeChecksum_is_deterministic()
    {
        var data = Encoding.UTF8.GetBytes("same input");

        var result1 = OstreeEncoders.ComputeChecksum(data);
        var result2 = OstreeEncoders.ComputeChecksum(data);

        result1.Should().Equal(result2);
    }

    [Fact]
    public void ChecksumToHex_produces_lowercase_hex_string()
    {
        var checksum = new byte[] { 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x9A };

        var result = OstreeEncoders.ChecksumToHex(checksum);

        result.Should().Be("abcdef123456789a");
    }

    [Fact]
    public void HexToChecksum_roundtrips_correctly()
    {
        var original = new byte[32];
        for (var i = 0; i < 32; i++)
        {
            original[i] = (byte)i;
        }

        var hex = OstreeEncoders.ChecksumToHex(original);
        var result = OstreeEncoders.HexToChecksum(hex);

        result.Should().Equal(original);
    }
}
