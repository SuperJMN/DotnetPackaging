using System.Buffers.Binary;
using System.IO.Hashing;
using System.Linq;
using System.Text;

namespace DotnetPackaging.Formats.Dmg.Udif
{
    /// <summary>
    /// Validates UDIF DMG file structure and integrity
    /// </summary>
    public class UdifValidator
    {
        public class ValidationException : Exception
        {
            public IReadOnlyList<string> Errors { get; }
            public IReadOnlyList<string> Warnings { get; }
            public ValidationException(IEnumerable<string> errors, IEnumerable<string> warnings)
                : base($"UDIF validation failed: {string.Join("; ", errors)}")
            {
                Errors = errors.ToList();
                Warnings = warnings.ToList();
            }
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public KolyBlock? KolyBlock { get; set; }
            public List<BlockDescriptor>? BlockDescriptors { get; set; }
        }

        public class BlockDescriptor
        {
            public uint Type { get; set; }
            public ulong SectorStart { get; set; }
            public ulong SectorCount { get; set; }
            public ulong CompressedOffset { get; set; }
            public ulong CompressedLength { get; set; }
        }

        private const int KolySize = 512;
        private const int SectorSize = 512;
        private const ulong MaxReasonableSegmentSize = 100UL * 1024 * 1024 * 1024; // 100 GB

        public void ValidateOrThrow(Stream dmgStream)
        {
            var res = Validate(dmgStream);
            if (!res.IsValid)
            {
                throw new ValidationException(res.Errors, res.Warnings);
            }
        }

        public ValidationResult Validate(Stream dmgStream)
        {
            var result = new ValidationResult();

            // Check minimum file size
            if (dmgStream.Length < KolySize)
            {
                result.Errors.Add($"File too small: {dmgStream.Length} bytes. Minimum is {KolySize} bytes (koly footer size).");
                return result;
            }

            // Read and validate Koly footer
            dmgStream.Seek(-KolySize, SeekOrigin.End);
            byte[] kolyBytes = new byte[KolySize];
            if (dmgStream.Read(kolyBytes, 0, KolySize) != KolySize)
            {
                result.Errors.Add("Failed to read koly footer");
                return result;
            }

            var koly = ParseKolyBlock(kolyBytes, result);
            if (koly == null)
            {
                return result;
            }

            result.KolyBlock = koly.Value;

            // Validate koly fields
            ValidateKolyBlock(koly.Value, dmgStream.Length, result);

            if (result.Errors.Count > 0)
            {
                return result;
            }

            ValidateChecksums(dmgStream, koly.Value, result);

            // Parse and validate XML plist and blkx table
            if (koly.Value.XmlLength > 0)
            {
                ValidateXmlAndBlkx(dmgStream, koly.Value, result);
            }
            else
            {
                result.Errors.Add("XML plist length is 0");
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        public ValidationResult Validate(byte[] dmgData)
        {
            using var ms = new MemoryStream(dmgData);
            return Validate(ms);
        }

        private KolyBlock? ParseKolyBlock(byte[] data, ValidationResult result)
        {
            try
            {
                var koly = new KolyBlock();

                koly.Signature = ReadBigEndianUInt32(data, 0);
                koly.Version = ReadBigEndianUInt32(data, 4);
                koly.HeaderSize = ReadBigEndianUInt32(data, 8);
                koly.Flags = ReadBigEndianUInt32(data, 12);
                koly.RunningDataForkOffset = ReadBigEndianUInt64(data, 16);
                koly.DataForkOffset = ReadBigEndianUInt64(data, 24);
                koly.DataForkLength = ReadBigEndianUInt64(data, 32);
                koly.RsrcForkOffset = ReadBigEndianUInt64(data, 40);
                koly.RsrcForkLength = ReadBigEndianUInt64(data, 48);
                koly.SegmentNumber = ReadBigEndianUInt32(data, 56);
                koly.SegmentCount = ReadBigEndianUInt32(data, 60);

                // SegmentId at 64 (16 bytes GUID)
                byte[] guidBytes = new byte[16];
                Array.Copy(data, 64, guidBytes, 0, 16);
                koly.SegmentId = new Guid(guidBytes);

                koly.DataForkChecksumType = ReadBigEndianUInt32(data, 80);
                koly.DataForkChecksumSize = ReadBigEndianUInt32(data, 84);
                koly.DataForkChecksum = new byte[128];
                Array.Copy(data, 88, koly.DataForkChecksum, 0, 128);

                koly.XmlOffset = ReadBigEndianUInt64(data, 0xD8); // 216
                koly.XmlLength = ReadBigEndianUInt64(data, 0xE0); // 224

                koly.Reserved1 = new byte[120];
                Array.Copy(data, 232, koly.Reserved1, 0, 120);

                koly.ChecksumType = ReadBigEndianUInt32(data, 352);
                koly.ChecksumSize = ReadBigEndianUInt32(data, 356);
                koly.Checksum = new byte[128];
                Array.Copy(data, 360, koly.Checksum, 0, 128);

                koly.ImageVariant = ReadBigEndianUInt32(data, 488);
                koly.SectorCount = ReadBigEndianUInt64(data, 492);
                koly.Reserved2 = ReadBigEndianUInt32(data, 500);
                koly.Reserved3 = ReadBigEndianUInt32(data, 504);
                koly.Reserved4 = ReadBigEndianUInt32(data, 508);

                return koly;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to parse koly block: {ex.Message}");
                return null;
            }
        }

        private void ValidateKolyBlock(KolyBlock koly, long fileSize, ValidationResult result)
        {
            // Validate signature
            if (koly.Signature != UdifConstants.KolySignature)
            {
                result.Errors.Add($"Invalid koly signature: 0x{koly.Signature:X8}. Expected 0x{UdifConstants.KolySignature:X8} ('koly')");
            }

            // Validate version
            if (koly.Version != 4)
            {
                result.Warnings.Add($"Unexpected version: {koly.Version}. Expected 4");
            }

            // Validate header size
            if (koly.HeaderSize != KolySize)
            {
                result.Errors.Add($"Invalid header size: {koly.HeaderSize}. Expected {KolySize}");
            }

            // Validate flags (bit 0 should be set for flattened images)
            if ((koly.Flags & 1) == 0)
            {
                result.Warnings.Add("Flags bit 0 not set (not marked as flattened image)");
            }

            // Validate DataFork
            if (koly.DataForkOffset != 0)
            {
                result.Warnings.Add($"DataForkOffset is {koly.DataForkOffset}, expected 0 for flattened images");
            }

            if (koly.DataForkLength > (ulong)fileSize)
            {
                result.Errors.Add($"DataForkLength ({koly.DataForkLength}) exceeds file size ({fileSize})");
            }

            if (koly.DataForkLength % SectorSize != 0)
            {
                result.Warnings.Add($"DataForkLength ({koly.DataForkLength}) is not aligned to {SectorSize} bytes");
            }

            // Validate segment info
            if (koly.SegmentNumber == 0)
            {
                result.Errors.Add("SegmentNumber is 0 (should be 1-based)");
            }

            if (koly.SegmentCount == 0)
            {
                result.Errors.Add("SegmentCount is 0");
            }

            if (koly.SegmentNumber > koly.SegmentCount)
            {
                result.Errors.Add($"SegmentNumber ({koly.SegmentNumber}) exceeds SegmentCount ({koly.SegmentCount})");
            }

            // Validate XML offset and length
            if (koly.XmlLength == 0)
            {
                result.Errors.Add("XmlLength is 0");
            }
            else
            {
                if (koly.XmlOffset + koly.XmlLength > (ulong)fileSize)
                {
                    result.Errors.Add($"XML region (offset {koly.XmlOffset}, length {koly.XmlLength}) extends beyond file size ({fileSize})");
                }

                if (koly.XmlOffset < koly.DataForkLength)
                {
                    result.Errors.Add($"XmlOffset ({koly.XmlOffset}) is within DataFork region (length {koly.DataForkLength})");
                }

                ulong expectedXmlOffset = koly.DataForkLength;
                if (koly.XmlOffset != expectedXmlOffset)
                {
                    result.Errors.Add($"XmlOffset ({koly.XmlOffset}) does not match expected position after DataFork ({expectedXmlOffset})");
                }

                // XML should end just before koly footer
                ulong expectedKolyStart = (ulong)(fileSize - KolySize);
                ulong xmlEnd = koly.XmlOffset + koly.XmlLength;
                if (xmlEnd != expectedKolyStart)
                {
                    result.Errors.Add($"XML end position ({xmlEnd}) does not match koly footer start ({expectedKolyStart})");
                }
            }

            // Validate SectorCount
            if (koly.SectorCount > 0)
            {
                ulong expectedDataSize = koly.SectorCount * SectorSize;
                if (expectedDataSize > MaxReasonableSegmentSize)
                {
                    result.Errors.Add($"SectorCount ({koly.SectorCount}) implies unreasonably large uncompressed size ({expectedDataSize} bytes)");
                }
            }
        }

        private void ValidateChecksums(Stream dmgStream, KolyBlock koly, ValidationResult result)
        {
            // Data fork checksum
            if (koly.DataForkChecksumType == 0 && koly.DataForkChecksumSize == 0)
            {
                result.Warnings.Add("DataFork checksum missing");
            }
            else if (koly.DataForkChecksumType == 2 && koly.DataForkChecksumSize == 4)
            {
                uint stored = ReadBigEndianUInt32(koly.DataForkChecksum, 0);
                uint computed = ComputeCrc32(dmgStream, 0, (long)koly.DataForkLength);
                if (stored != computed)
                {
                    result.Errors.Add($"DataFork checksum mismatch: stored 0x{stored:X8} vs computed 0x{computed:X8}");
                }
            }
            else
            {
                result.Warnings.Add($"Unsupported DataFork checksum type/size: {koly.DataForkChecksumType}/{koly.DataForkChecksumSize}");
            }

            // Master checksum covers data fork + XML
            ulong masterLength = koly.XmlOffset + koly.XmlLength;
            if (koly.ChecksumType == 0 && koly.ChecksumSize == 0)
            {
                result.Warnings.Add("Master checksum missing");
            }
            else if (koly.ChecksumType == 2 && koly.ChecksumSize == 4)
            {
                uint stored = ReadBigEndianUInt32(koly.Checksum, 0);
                uint computed = ComputeCrc32(dmgStream, 0, (long)masterLength);
                if (stored != computed)
                {
                    result.Errors.Add($"Master checksum mismatch: stored 0x{stored:X8} vs computed 0x{computed:X8}");
                }
            }
            else
            {
                result.Warnings.Add($"Unsupported master checksum type/size: {koly.ChecksumType}/{koly.ChecksumSize}");
            }
        }

        private void ValidateXmlAndBlkx(Stream dmgStream, KolyBlock koly, ValidationResult result)
        {
            try
            {
                // Read XML plist
                dmgStream.Seek((long)koly.XmlOffset, SeekOrigin.Begin);
                byte[] xmlBytes = new byte[koly.XmlLength];
                if (dmgStream.Read(xmlBytes, 0, (int)koly.XmlLength) != (int)koly.XmlLength)
                {
                    result.Errors.Add("Failed to read XML plist");
                    return;
                }

                string xmlContent = Encoding.UTF8.GetString(xmlBytes);

                // Validate XML structure
                if (!xmlContent.Contains("<?xml"))
                {
                    result.Errors.Add("XML plist does not start with XML declaration");
                }

                if (!xmlContent.Contains("<key>blkx</key>"))
                {
                    result.Errors.Add("XML plist does not contain blkx key");
                    return;
                }

                // Extract and validate mish block
                byte[]? mishData = ExtractMishBlock(xmlContent, result);
                if (mishData != null)
                {
                    ValidateMishBlock(mishData, koly, dmgStream.Length, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error validating XML/blkx: {ex.Message}");
            }
        }

        private byte[]? ExtractMishBlock(string plistText, ValidationResult result)
        {
            try
            {
                int dataKeyIndex = plistText.IndexOf("<key>Data</key>");
                if (dataKeyIndex < 0)
                {
                    result.Errors.Add("No Data key found in plist");
                    return null;
                }

                int dataTagStart = plistText.IndexOf("<data>", dataKeyIndex);
                int dataTagEnd = plistText.IndexOf("</data>", dataTagStart);

                if (dataTagStart < 0 || dataTagEnd < 0)
                {
                    result.Errors.Add("Malformed Data tag in plist");
                    return null;
                }

                string base64Data = plistText.Substring(dataTagStart + 6, dataTagEnd - dataTagStart - 6)
                    .Replace("\n", "").Replace("\t", "").Replace(" ", "").Replace("\r", "");

                return Convert.FromBase64String(base64Data);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to extract mish block: {ex.Message}");
                return null;
            }
        }

        private void ValidateMishBlock(byte[] mishData, KolyBlock koly, long fileSize, ValidationResult result)
        {
            if (mishData.Length < 0xCC)
            {
                result.Errors.Add($"Mish block too small: {mishData.Length} bytes");
                return;
            }

            // Parse mish header
            uint signature = ReadBigEndianUInt32(mishData, 0);
            if (signature != UdifConstants.BlockSignature)
            {
                result.Errors.Add($"Invalid mish signature: 0x{signature:X8}. Expected 0x{UdifConstants.BlockSignature:X8} ('mish')");
            }

            uint version = ReadBigEndianUInt32(mishData, 4);
            ulong startSector = ReadBigEndianUInt64(mishData, 8);
            ulong sectorCount = ReadBigEndianUInt64(mishData, 16);
            ulong dataOffset = ReadBigEndianUInt64(mishData, 24);
            uint buffersNeeded = ReadBigEndianUInt32(mishData, 32);
            uint blockDescriptors = ReadBigEndianUInt32(mishData, 36);

            // Validate buffers needed
            if (buffersNeeded == 0)
            {
                result.Errors.Add("BuffersNeeded is 0");
            }
            else if (buffersNeeded > 1000000)
            {
                result.Errors.Add($"BuffersNeeded is unreasonably large: {buffersNeeded}");
            }

            // Read block run count
            uint blockRunCount = ReadBigEndianUInt32(mishData, 0xC8);
            if (blockRunCount == 0)
            {
                result.Errors.Add("Block run count is 0");
                return;
            }

            if (blockRunCount > 100000)
            {
                result.Errors.Add($"Block run count is unreasonably large: {blockRunCount}");
                return;
            }

            // Validate block descriptors
            result.BlockDescriptors = new List<BlockDescriptor>();
            var descriptors = result.BlockDescriptors;
            int blockOffset = 0xCC;

            for (int i = 0; i < blockRunCount; i++)
            {
                if (blockOffset + 40 > mishData.Length)
                {
                    result.Errors.Add($"Block descriptor {i} extends beyond mish data");
                    break;
                }

                var block = new BlockDescriptor
                {
                    Type = ReadBigEndianUInt32(mishData, blockOffset),
                    SectorStart = ReadBigEndianUInt64(mishData, blockOffset + 8),
                    SectorCount = ReadBigEndianUInt64(mishData, blockOffset + 16),
                    CompressedOffset = ReadBigEndianUInt64(mishData, blockOffset + 24),
                    CompressedLength = ReadBigEndianUInt64(mishData, blockOffset + 32)
                };

                descriptors.Add(block);

                // Skip terminator block validation
                if (block.Type == 0xFFFFFFFF)
                {
                    blockOffset += 40;
                    continue;
                }

                // Validate block alignment
                if (block.CompressedOffset % SectorSize != 0)
                {
                    result.Errors.Add($"Block {i}: CompressedOffset ({block.CompressedOffset}) is not aligned to {SectorSize} bytes");
                }

                // Validate block is within file bounds
                if (block.CompressedOffset + block.CompressedLength > (ulong)fileSize)
                {
                    result.Errors.Add($"Block {i}: Compressed data (offset {block.CompressedOffset}, length {block.CompressedLength}) extends beyond file size ({fileSize})");
                }

                // Validate block doesn't overlap with XML or koly
                if (block.CompressedOffset >= koly.XmlOffset)
                {
                    result.Errors.Add($"Block {i}: CompressedOffset ({block.CompressedOffset}) overlaps with XML/footer region (starts at {koly.XmlOffset})");
                }

                // Check for unreasonable sizes
                if (block.CompressedLength > MaxReasonableSegmentSize)
                {
                    result.Errors.Add($"Block {i}: CompressedLength ({block.CompressedLength}) is unreasonably large (would require allocating {block.CompressedLength} bytes)");
                }

                if (block.SectorCount > MaxReasonableSegmentSize / SectorSize)
                {
                    result.Errors.Add($"Block {i}: SectorCount ({block.SectorCount}) is unreasonably large");
                }

                blockOffset += 40;
            }

            var terminator = descriptors.LastOrDefault(b => b.Type == 0xFFFFFFFF);
            if (terminator == null)
            {
                result.Errors.Add("Missing terminator block (0xFFFFFFFF) in block run");
            }
            else
            {
                if (terminator.CompressedOffset != koly.XmlOffset)
                {
                    result.Errors.Add($"Terminator block offset ({terminator.CompressedOffset}) does not match XML start ({koly.XmlOffset})");
                }

                if (terminator.CompressedLength != 0)
                {
                    result.Warnings.Add("Terminator block length is non-zero");
                }
            }
        }

        private uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private ulong ReadBigEndianUInt64(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) |
                   ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                   ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) |
                   ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private uint ComputeCrc32(Stream stream, long offset, long length)
        {
            long originalPosition = stream.Position;
            stream.Seek(offset, SeekOrigin.Begin);

            var crc = new Crc32();
            byte[] buffer = new byte[81920];
            long remaining = length;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = stream.Read(buffer, 0, toRead);
                if (read <= 0)
                {
                    throw new InvalidOperationException("Unexpected end of stream while computing CRC32");
                }

                crc.Append(buffer.AsSpan(0, read));
                remaining -= read;
            }

            stream.Seek(originalPosition, SeekOrigin.Begin);
            var hash = crc.GetCurrentHash();
            return BinaryPrimitives.ReadUInt32BigEndian(hash);
        }
    }
}
