using BlockCompressor;

namespace DotnetPackaging.Msix.Core.BlockMap;

public record FileBlockInfo(MsixEntry Entry, IList<DeflateBlock> Blocks);