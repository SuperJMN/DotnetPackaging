namespace DotnetPackaging.Msix.Core.Compression;

internal class MsixBlock
{
    public required byte[] OriginalData { get; init; }
    public required byte[] CompressedData { get; init; }
}
