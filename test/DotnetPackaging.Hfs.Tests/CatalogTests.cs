using DotnetPackaging.Hfs.Catalog;
using DotnetPackaging.Hfs.Encoding;
using FluentAssertions;
using Xunit;

namespace DotnetPackaging.Hfs.Tests;

public class CatalogTests
{
    [Fact]
    public void CatalogKey_ShouldEncodeCorrectly()
    {
        var key = CatalogKey.For(CatalogNodeId.RootFolder, "test.txt");
        var bytes = key.ToBytes();
        
        // Should have: keyLength(2) + parentID(4) + nameLength(2) + name(UTF-16BE)
        bytes.Length.Should().BeGreaterThan(8);
    }

    [Fact]
    public void CatalogFolderRecord_ShouldBe88Bytes()
    {
        var record = new CatalogFolderRecord
        {
            FolderId = CatalogNodeId.RootFolder,
            Valence = 0
        };
        
        var bytes = record.ToBytes();
        bytes.Length.Should().Be(88);
    }

    [Fact]
    public void CatalogFileRecord_ShouldBe248Bytes()
    {
        var record = new CatalogFileRecord
        {
            FileId = new CatalogNodeId(16)
        };
        
        var bytes = record.ToBytes();
        bytes.Length.Should().Be(248);
    }

    [Fact]
    public void CatalogBTree_ShouldAddRootFolder()
    {
        var catalog = new CatalogBTree();
        catalog.AddRootFolder("TestVolume", 5);
        
        // Should have header node + leaf node with root folder + thread
        catalog.BTree.Header.LeafRecords.Should().Be(2);
    }

    [Fact]
    public void HfsUnicode_ShouldEncodeToUtf16BE()
    {
        var encoded = HfsUnicode.Encode("test");
        
        // "test" = 4 chars * 2 bytes = 8 bytes in UTF-16
        encoded.Length.Should().Be(8);
        
        // 't' in UTF-16 BE is 0x00 0x74
        encoded[0].Should().Be(0x00);
        encoded[1].Should().Be(0x74);
    }

    [Fact]
    public void HfsUnicode_ShouldApplyNFDNormalization()
    {
        // é can be represented as single char (U+00E9) or decomposed (e + combining acute)
        var composed = "é";
        var encoded = HfsUnicode.Encode(composed);
        var decoded = HfsUnicode.Decode(encoded);
        
        // After NFD normalization, should be decomposed
        decoded.IsNormalized(System.Text.NormalizationForm.FormD).Should().BeTrue();
    }
}
