namespace DotnetPackaging.Msix.Core;

public class MsixEntry
{
    public required string FullPath { get; init; }
    public required CompressionLevel CompressionLevel { get; init; }
    public required IByteSource Original { get; init; }
    public required IByteSource Compressed { get; init; }
    public DateTime ModificationTime { get; set; } = DateTime.Now;
    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }
    public uint Crc32 { get; set; }
    public long LocalHeaderOffset { get; set; }
    public bool MetadataCalculated { get; set; }

    public override string ToString()
    {
        return FullPath;
    }
}
