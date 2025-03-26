namespace MsixPackaging.Core.BlockMap;

public class CompressionBlockInfo
{
    public int BlockIndex { get; set; }
    public int CompressedSize { get; set; }
    public string BlockHash { get; set; }
}