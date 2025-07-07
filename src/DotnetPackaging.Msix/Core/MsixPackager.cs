using System.Text;
using BlockCompressor;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core.BlockMap;
using DotnetPackaging.Msix.Core.Compression;
using DotnetPackaging.Msix.Core.ContentTypes;
using Zafiro.Mixins;
using Zafiro.Reactive;

namespace DotnetPackaging.Msix.Core;

public class MsixPackager(Maybe<ILogger> logger)
{
    public Result<IByteSource> Pack(INamedContainer container)
    {
        return Result.Success()
            .Map(container.FilesWithPathsRecursive)
            .Map(Compress);
    }

    private IByteSource Compress(IEnumerable<INamedByteSourceWithPath> files)
    {
        return ByteSource.FromAsyncStreamFactory(() => GetStream(files.ToList()));
    }

    private async Task<Stream> GetStream(IList<INamedByteSourceWithPath> files)
    {
        var zipStream = new MemoryStream();
        
        await using (var zipper = new MsixBuilder(zipStream, logger))
        {
            await WritePayload(files, zipper);
            await WriteContentTypes(files, zipper);
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
            
            if (file.Name.EndsWith(".png"))
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
            
            await msix.PutNextEntry(entry);
            
            logger.Debug("Added entry for {File}", file.FullPath());
            
            var fileBlockInfo = new FileBlockInfo(entry, blocks);
            blockInfos.Add(fileBlockInfo);
        }
        
        await AddBlockMap(msix, blockInfos);
    }

    private async Task AddBlockMap(MsixBuilder msix, List<FileBlockInfo> blockInfos)
    {
        logger.Debug("Adding Block Map");
        var blockMapModel = new BlockMapModel("SHA256", blockInfos.ToImmutableList());
        logger.Debug("Serializing block map");
        var blockMapXml = await new BlockMapSerializer(logger).GenerateBlockMapXml(blockMapModel);
        logger.Debug("Block map serialized");
        
        logger.Debug("Adding Block Map entry to package");
        await msix.PutNextEntry(MsixEntryFactory.Compress("AppxBlockMap.xml", ByteSource.FromString(blockMapXml, Encoding.UTF8)));
        logger.Debug("Block map added");
    }
}