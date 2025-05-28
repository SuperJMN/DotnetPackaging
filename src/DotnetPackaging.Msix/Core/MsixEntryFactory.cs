using DotnetPackaging.Msix.Core.Compression;

namespace DotnetPackaging.Msix.Core;

public static class MsixEntryFactory
{
    public static MsixEntry Compress(string entryName, IByteSource data)
    {
        var compressionLevel = CompressionLevel.Optimal;

        var msixEntry = new MsixEntry
        {
            Original = data,
            Compressed = ByteSource.FromByteObservable(data.Bytes.Compressed()),
            FullPath = entryName,
            CompressionLevel = compressionLevel,
        };

        return msixEntry;
    }
}