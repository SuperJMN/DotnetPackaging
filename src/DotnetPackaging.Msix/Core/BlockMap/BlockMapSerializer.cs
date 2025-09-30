using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using BlockCompressor;
using CSharpFunctionalExtensions;
using Zafiro.Mixins;

namespace DotnetPackaging.Msix.Core.BlockMap;

public class BlockMapSerializer(Maybe<ILogger> logger)
{
    private const string DefaultNamespace = "http://schemas.microsoft.com/appx/2010/blockmap";
    private const string BlockMap2021Namespace = "http://schemas.microsoft.com/appx/2021/blockmap";
    private const string HashMethod = "http://www.w3.org/2001/04/xmlenc#sha256";

    public string GenerateBlockMapXml(BlockMapModel model)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\r\n");
        sb.Append($"<BlockMap xmlns=\"{DefaultNamespace}\" xmlns:b4=\"{BlockMap2021Namespace}\" IgnorableNamespaces=\"b4\" HashMethod=\"{HashMethod}\">");

        foreach (var fileInfo in model.Files)
        {
            var fileName = EscapeAttribute(fileInfo.Entry.FullPath.Replace("/", "\\"));
            var lfhSize = GetLhsSize(fileInfo.Entry);
            var size = fileInfo.Entry.UncompressedSize;

            if (fileInfo.Blocks.Count == 0)
            {
                sb.Append($"<File Name=\"{fileName}\" Size=\"{size}\" LfhSize=\"{lfhSize}\"/>");
                continue;
            }

            sb.Append($"<File Name=\"{fileName}\" Size=\"{size}\" LfhSize=\"{lfhSize}\">");

            foreach (var block in fileInfo.Blocks)
            {
                var blockHash = Convert.ToBase64String(SHA256.HashData(block.OriginalData));
                sb.Append($"<Block Hash=\"{blockHash}\"");

                if (fileInfo.Entry.CompressionLevel != CompressionLevel.NoCompression)
                {
                    sb.Append($" Size=\"{block.CompressedData.Length}\"");
                }

                sb.Append("/>");
            }

            if (fileInfo.Blocks.Count > 1)
            {
                var fileHash = ComputeFileHash(fileInfo.Blocks);
                sb.Append($"<b4:FileHash Hash=\"{fileHash}\"/>");
            }

            sb.Append("</File>");
        }

        sb.Append("</BlockMap>");
        return sb.ToString();
    }

    private static string EscapeAttribute(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private static string ComputeFileHash(IList<DeflateBlock> blocks)
    {
        using var sha = SHA256.Create();
        foreach (var block in blocks)
        {
            if (block.OriginalData.Length > 0)
            {
                sha.TransformBlock(block.OriginalData, 0, block.OriginalData.Length, null, 0);
            }
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = sha.Hash ?? Array.Empty<byte>();
        return Convert.ToBase64String(hash);
    }

    private static int GetLhsSize(MsixEntry entry)
    {
        return 30 + Encoding.UTF8.GetByteCount(entry.FullPath);
    }
}
