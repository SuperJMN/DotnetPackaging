using BlockCompressor;

namespace DotnetPackaging.Msix.Core.BlockMap;

internal record FileBlockInfo(MsixEntry Entry, IList<DeflateBlock> Blocks);
