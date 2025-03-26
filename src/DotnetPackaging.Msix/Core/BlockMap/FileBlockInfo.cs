using BlockCompressor;

namespace MsixPackaging.Core.BlockMap;

public record FileBlockInfo(MsixEntry Entry, IList<DeflateBlock> Blocks);