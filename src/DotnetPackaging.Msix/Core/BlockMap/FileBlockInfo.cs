using DotnetPackaging.Msix.Core.Compression;

namespace DotnetPackaging.Msix.Core.BlockMap;

internal record FileBlockInfo(MsixEntry Entry, IList<MsixBlock> Blocks);
