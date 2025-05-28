namespace DotnetPackaging.Msix.Core;

public class MsixEntry
{
    public required string FullPath { get; init; }
    public required CompressionLevel CompressionLevel { get; init; }
    public required IByteSource Original { get; init; }
    public required IByteSource Compressed { get; init; }
    public DateTime ModificationTime { get; set; } = DateTime.Now;

    public override string ToString()
    {
        return FullPath;
    }
}
