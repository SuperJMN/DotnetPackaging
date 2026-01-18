using System.Buffers.Binary;
using DotnetPackaging.Rpm.Builder;
using FluentAssertions;

namespace DotnetPackaging.Rpm.Tests;

public class RpmHeaderStructureTests
{
    [Fact]
    public void BuildWithRegion_ShouldCreateCorrectTrailerStructure()
    {
        // Arrange
        var entries = new List<RpmHeaderEntry>
        {
            RpmHeaderEntry.Int32(1000, 12345), // Dummy entry
        };
        int regionTag = 62;

        // Act
        var headerBytes = RpmHeaderBuilder.BuildWithRegion(entries, regionTag);

        // Assert
        // Header starts with magic + 4 reserved + 4 count + 4 size
        // Magic 8E AD E8 01
        headerBytes[0].Should().Be(0x8E);
        headerBytes[1].Should().Be(0xAD);
        headerBytes[2].Should().Be(0xE8);
        headerBytes[3].Should().Be(0x01);

        // Get Index Count
        var indexCount = BinaryPrimitives.ReadInt32BigEndian(headerBytes.AsSpan(8, 4));
        // Expect entries.Count + 1 (region tag)
        indexCount.Should().Be(entries.Count + 1);

        // Parse Index Entries to find Region Tag
        int indexStart = 16;
        int entrySize = 16;
        
        bool foundRegion = false;
        int regionOffset = 0;

        for (int i = 0; i < indexCount; i++)
        {
            var entrySpan = headerBytes.AsSpan(indexStart + (i * entrySize), entrySize);
            var tag = BinaryPrimitives.ReadInt32BigEndian(entrySpan.Slice(0, 4));
            var type = BinaryPrimitives.ReadInt32BigEndian(entrySpan.Slice(4, 4));
            var offset = BinaryPrimitives.ReadInt32BigEndian(entrySpan.Slice(8, 4));
            var count = BinaryPrimitives.ReadInt32BigEndian(entrySpan.Slice(12, 4));

            if (tag == regionTag)
            {
                foundRegion = true;
                type.Should().Be(7); // BIN
                count.Should().Be(16); // Size of trailer data
                regionOffset = offset;
            }
        }

        foundRegion.Should().BeTrue("Region tag should be present in index");

        // Verify Data Store contains the Trailer at regionOffset
        // Valid header structure usually has data store immediately after index entries
        int dataStart = indexStart + (indexCount * entrySize);
        var trailerSpan = headerBytes.AsSpan(dataStart + regionOffset, 16);

        // Trailer check
        var trailerTag = BinaryPrimitives.ReadInt32BigEndian(trailerSpan.Slice(0, 4));
        var trailerType = BinaryPrimitives.ReadInt32BigEndian(trailerSpan.Slice(4, 4));
        var trailerNegOffset = BinaryPrimitives.ReadInt32BigEndian(trailerSpan.Slice(8, 4));
        var trailerCount = BinaryPrimitives.ReadInt32BigEndian(trailerSpan.Slice(12, 4));

        trailerTag.Should().Be(regionTag);
        trailerType.Should().Be(7); // BIN
        
        // Negative offset verification
        // It should point back to the start of the index entries relative to the *start of data*?
        // Spec says: "The offset is negative and points to the start of the header index."
        // Usually it's -(indexCount * 16)
        trailerNegOffset.Should().Be(-(indexCount * 16));
        
        trailerCount.Should().Be(16); // This was the bug fix (was 5)
    }

    [Fact]
    public void BuildMetadataHeader_ShouldIncludeTag63()
    {
        // Act
        // Create minimal metadata
        // Act
        // Create minimal metadata
        var metadata = new PackageMetadata("Check", Architecture.X64, false, "test-pkg", "1.0.0")
        {
            Name = "Check",
            Architecture = Architecture.X64,
            Package = "test-pkg",
            Version = "1.0.0",
            ModificationTime = DateTimeOffset.Now,
            License = "MIT",
            Summary = "Summary",
            Description = "Desc",
            Vendor = "Vendor",
            Url = new Uri("http://url")
        };
        var fileList = new RpmFileList(new List<RpmFileEntry>(), new List<string>());
        
        var headerBytes = RpmHeaderWriter.BuildMetadataHeader(metadata, fileList, 0, Array.Empty<byte>());
        
        // Assert
        // Scan for Tag 63 in index
        var indexCount = BinaryPrimitives.ReadInt32BigEndian(headerBytes.AsSpan(8, 4));
        int indexStart = 16;
        int entrySize = 16;
        bool foundTag63 = false;

        for (int i = 0; i < indexCount; i++)
        {
            var entrySpan = headerBytes.AsSpan(indexStart + (i * entrySize), entrySize);
            var tag = BinaryPrimitives.ReadInt32BigEndian(entrySpan.Slice(0, 4));
            if (tag == 63) foundTag63 = true;
        }

        foundTag63.Should().BeTrue("Metadata Header must include HeaderImmutable tag (63)");
    }
}
