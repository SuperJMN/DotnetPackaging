using System.Collections.Generic;
using System.IO;
using System.Text;
using BlockCompressor;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core.BlockMap;
using DotnetPackaging.Msix.Core.Compression;
using DotnetPackaging.Msix.Core.ContentTypes;
using Zafiro.Mixins;
using Zafiro.Reactive;

namespace DotnetPackaging.Msix.Core;

internal class MsixPackager(Maybe<ILogger> logger)
{
    private static readonly HashSet<string> NonCompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "avif","avi","bmp","cab","cer","deb","divx","dvr-ms","flac","gif","gz","ico",
        "jpeg","jpg","m4a","m4v","mkv","mov","mp3","mp4","mpeg","mpg","msi","msix",
        "msixbundle","msm","msp","oga","ogg","ogv","opus","pdf","png","pfx","pri","svg",
        "swf","tgz","tif","tiff","wav","webm","webp","wma","wmv","woff","woff2","xap",
        "xbap","zip","zst","7z","rar","bz2","xz","lzma"
    };

    private static readonly IComparer<INamedByteSourceWithPath> FileOrderingComparer =
        Comparer<INamedByteSourceWithPath>.Create(CompareFiles);

    public Result<IByteSource> Pack(IContainer container)
    {
        return Result.Success()
            .Map(() => container.ResourcesWithPathsRecursive())
            .Map(Compress);
    }

    private IByteSource Compress(IEnumerable<INamedByteSourceWithPath> files)
    {
        return ByteSource.FromAsyncStreamFactory(() => GetStream(files.ToList()));
    }

    private async Task<Stream> GetStream(IList<INamedByteSourceWithPath> files)
    {
        var zipStream = new MemoryStream();
        var orderedFiles = files
            .OrderBy(file => file, FileOrderingComparer)
            .ToList();

        await using (var zipper = new MsixBuilder(zipStream, logger))
        {
            await WritePayload(orderedFiles, zipper);
            await WriteContentTypes(orderedFiles, zipper);
        }

        var finalStream = new MemoryStream();
        zipStream.Position = 0;
        await zipStream.CopyToAsync(finalStream);

        finalStream.Position = 0;
        return finalStream;
    }

    private static async Task WriteContentTypes(IEnumerable<INamedByteSourceWithPath> files, MsixBuilder msix)
    {
        var contentTypes = ContentTypesGenerator.Create(files.Select(x => x.Name).Append("AppxBlockMap.xml"));
        var xml = ContentTypesSerializer.Serialize(contentTypes);

        await msix.PutNextEntry(MsixEntryFactory.Compress("[Content_Types].xml", ByteSource.FromString(xml, Encoding.UTF8)));
    }

    private async Task WritePayload(IEnumerable<INamedByteSourceWithPath> files, MsixBuilder msix)
    {
        var blockInfos = new List<FileBlockInfo>();

        foreach (var file in files)
        {
            logger.Debug("Processing {File}", file.FullPath());
            MsixEntry entry;
            IList<DeflateBlock> blocks;

            if (ShouldStore(file.Name))
            {
                entry = new MsixEntry()
                {
                    Compressed = file,
                    Original = file,
                    FullPath = file.FullPath(),
                    CompressionLevel = CompressionLevel.NoCompression,
                };

                blocks = await file.Bytes.Flatten().Buffer(64 * 1024).Select(list => new DeflateBlock
                {
                    CompressedData = list.ToArray(),
                    OriginalData = list.ToArray(),
                }).ToList();
            }
            else
            {
                var compressionBlocks = file.Bytes.CompressionBlocks();
                entry = new MsixEntry
                {
                    Original = file,
                    Compressed = ByteSource.FromByteObservable(compressionBlocks.Select(x => x.CompressedData)),
                    FullPath = file.FullPath(),
                    CompressionLevel = CompressionLevel.Optimal,
                };

                blocks = await compressionBlocks.ToList();
            }

            PopulateEntryMetadata(entry, blocks);

            await msix.PutNextEntry(entry);

            logger.Debug("Added entry for {File}", file.FullPath());

            var fileBlockInfo = new FileBlockInfo(entry, blocks);
            blockInfos.Add(fileBlockInfo);
        }

        await AddBlockMap(msix, blockInfos);
    }

    private static void PopulateEntryMetadata(MsixEntry entry, IList<DeflateBlock> blocks)
    {
        var crc32 = new System.IO.Hashing.Crc32();
        long uncompressed = 0;
        long compressed = 0;

        foreach (var block in blocks)
        {
            if (block.OriginalData != null)
            {
                crc32.Append(block.OriginalData);
                uncompressed += block.OriginalData.Length;
            }

            if (block.CompressedData != null)
            {
                compressed += block.CompressedData.Length;
            }
        }

        entry.Crc32 = crc32.GetCurrentHashAsUInt32();
        entry.UncompressedSize = uncompressed;
        entry.CompressedSize = compressed;
        entry.ModificationTime = DateTime.UtcNow.ToLocalTime();
        entry.MetadataCalculated = true;
    }

    private static bool ShouldStore(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return NonCompressibleExtensions.Contains(extension.TrimStart('.'));
    }

    private static int FileDepth(INamedByteSourceWithPath file)
    {
        var fullPath = file.FullPath().ToString().Replace('\\', '/');
        int depth = 0;
        foreach (var ch in fullPath)
        {
            if (ch == '/')
            {
                depth++;
            }
        }

        return depth;
    }

    private static string FileExtension(INamedByteSourceWithPath file)
    {
        var extension = System.IO.Path.GetExtension(file.Name);
        return string.IsNullOrEmpty(extension) ? string.Empty : extension.TrimStart('.');
    }

    private static int CompareFiles(INamedByteSourceWithPath? left, INamedByteSourceWithPath? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int depthComparison = FileDepth(right).CompareTo(FileDepth(left));
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        int extensionComparison = string.Compare(FileExtension(left), FileExtension(right), StringComparison.OrdinalIgnoreCase);
        if (extensionComparison != 0)
        {
            return extensionComparison;
        }

        return CompareNatural(left.FullPath().ToString(), right.FullPath().ToString());
    }

    private static int CompareNatural(string left, string right)
    {
        int indexLeft = 0;
        int indexRight = 0;

        while (indexLeft < left.Length && indexRight < right.Length)
        {
            char charLeft = left[indexLeft];
            char charRight = right[indexRight];

            if (char.IsDigit(charLeft) && char.IsDigit(charRight))
            {
                long numberLeft = 0;
                while (indexLeft < left.Length && char.IsDigit(left[indexLeft]))
                {
                    numberLeft = numberLeft * 10 + (left[indexLeft] - '0');
                    indexLeft++;
                }

                long numberRight = 0;
                while (indexRight < right.Length && char.IsDigit(right[indexRight]))
                {
                    numberRight = numberRight * 10 + (right[indexRight] - '0');
                    indexRight++;
                }

                if (numberLeft != numberRight)
                {
                    return numberLeft.CompareTo(numberRight);
                }
            }
            else
            {
                var comparison = char.ToUpperInvariant(charLeft).CompareTo(char.ToUpperInvariant(charRight));
                if (comparison != 0)
                {
                    return comparison;
                }

                indexLeft++;
                indexRight++;
            }
        }

        return (left.Length - indexLeft).CompareTo(right.Length - indexRight);
    }

    private async Task AddBlockMap(MsixBuilder msix, List<FileBlockInfo> blockInfos)
    {
        logger.Debug("Adding Block Map");
        var blockMapModel = new BlockMapModel("SHA256", blockInfos.ToImmutableList());
        logger.Debug("Serializing block map");
        var blockMapXml = new BlockMapSerializer(logger).GenerateBlockMapXml(blockMapModel);
        logger.Debug("Block map serialized");

        logger.Debug("Adding Block Map entry to package");
        await msix.PutNextEntry(MsixEntryFactory.Compress("AppxBlockMap.xml", ByteSource.FromString(blockMapXml, Encoding.UTF8)));
        logger.Debug("Block map added");
    }
}
