using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using SharpCompress.Compressors.BZip2;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Formats.Dmg.Udif
{
    public class UdifWriter
    {
        private const int ChunkSize = 0x100000; // 1MB chunks usually
        private const int SectorSize = 512;

        /// <summary>
        /// Compression type to use for the DMG. Default is Zlib (UDZO)
        /// </summary>
        public CompressionType CompressionType { get; set; } = CompressionType.Zlib;

        public void Create(IByteSource input, Stream output)
        {
            using (var inputStream = input.ToStream())
            {
                Create(inputStream, output);
            }
        }

        public void Create(Stream input, Stream output)
        {
            // 1. Compress Data Fork (absolute offsets, 512-aligned blocks)
            var blockMap = new List<BlockEntry>();
            long dataStart = output.Position; // absolute start of data fork in file
            var dataCrc = new Crc32();
            var masterCrc = new Crc32();

            byte[] buffer = new byte[ChunkSize];
            long inputOffset = 0; // uncompressed bytes processed
            ulong uncompressedSectorsWritten = 0; // for sector-based offsets

            while (true)
            {
                int read = input.Read(buffer, 0, ChunkSize);
                if (read == 0) break;

                // Compress chunk
                byte[] compressed = CompressChunk(buffer, read);

                // Align next block start to 512 bytes
                long alignPad = AlignUp(output.Position, SectorSize) - output.Position;
                if (alignPad > 0)
                {
                    output.Write(new byte[alignPad], 0, (int)alignPad);
                }

                long blockStartAbsolute = output.Position; // absolute offset in file

                // Write compressed data
                output.Write(compressed, 0, compressed.Length);
                dataCrc.Append(compressed);
                masterCrc.Append(compressed);

                // Compute sector counts (ceil for last partial chunk)
                ulong sectorsInChunk = (ulong)((read + SectorSize - 1) / SectorSize);

                // Add to block map
                blockMap.Add(new BlockEntry
                {
                    Type = (uint)CompressionType,
                    UncompressedOffset = uncompressedSectorsWritten,
                    UncompressedLength = sectorsInChunk,
                    CompressedOffset = (ulong)blockStartAbsolute,
                    CompressedLength = (ulong)compressed.Length
                });

                uncompressedSectorsWritten += sectorsInChunk;
                inputOffset += read;
            }

            // Align end of data fork to sector boundary before writing XML
            long xmlStart = AlignUp(output.Position, SectorSize);
            long pad = xmlStart - output.Position;
            if (pad > 0)
            {
                Span<byte> padding = pad <= 4096 ? stackalloc byte[(int)pad] : new byte[pad];
                padding.Clear();
                output.Write(padding);
                dataCrc.Append(padding);
                masterCrc.Append(padding);
            }

            // Add terminator block now that we know the aligned XML start
            blockMap.Add(new BlockEntry
            {
                Type = 0xFFFFFFFF,
                UncompressedOffset = uncompressedSectorsWritten,
                UncompressedLength = 0,
                CompressedOffset = (ulong)xmlStart,
                CompressedLength = 0
            });

            long dataForkLength = xmlStart - dataStart;

            // 2. Generate XML Plist
            string xml = GeneratePlist(blockMap, (ulong)input.Length);
            byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);

            long xmlOffsetAbsolute = output.Position; // absolute offset for koly (already aligned)
            output.Write(xmlBytes, 0, xmlBytes.Length);
            masterCrc.Append(xmlBytes);

            // 3. Write Koly Block (all big-endian, absolute offsets)
            var koly = new KolyBlock();
            koly.Signature = UdifConstants.KolySignature; // 'koly'
            koly.Version = 4;
            koly.HeaderSize = 512;
            koly.Flags = 1; // Bit 0 set: flattened image
            koly.RunningDataForkOffset = 0;
            koly.DataForkOffset = 0; // flattened
            koly.DataForkLength = (ulong)dataForkLength;
            koly.RsrcForkOffset = 0;
            koly.RsrcForkLength = 0;
            koly.SegmentNumber = 1;
            koly.SegmentCount = 1;
            koly.SegmentId = Guid.NewGuid();

            koly.XmlOffset = (ulong)xmlOffsetAbsolute; // absolute offset from start of file
            koly.XmlLength = (ulong)xmlBytes.Length;

            koly.SectorCount = (ulong)((input.Length + SectorSize - 1) / SectorSize); // total sectors (ceil)

            // Checksums (CRC32)
            uint dataForkCrc = BinaryPrimitives.ReadUInt32BigEndian(dataCrc.GetCurrentHash());
            koly.DataForkChecksumType = 2;
            koly.DataForkChecksumSize = 4;
            koly.DataForkChecksum = new byte[128];
            koly.DataForkChecksum[0] = (byte)(dataForkCrc >> 24);
            koly.DataForkChecksum[1] = (byte)(dataForkCrc >> 16);
            koly.DataForkChecksum[2] = (byte)(dataForkCrc >> 8);
            koly.DataForkChecksum[3] = (byte)dataForkCrc;

            uint masterCrcValue = BinaryPrimitives.ReadUInt32BigEndian(masterCrc.GetCurrentHash());
            koly.ChecksumType = 2;
            koly.ChecksumSize = 4;
            koly.Checksum = new byte[128];
            koly.Checksum[0] = (byte)(masterCrcValue >> 24);
            koly.Checksum[1] = (byte)(masterCrcValue >> 16);
            koly.Checksum[2] = (byte)(masterCrcValue >> 8);
            koly.Checksum[3] = (byte)masterCrcValue;

            WriteKoly(output, koly);
        }

        private byte[] CompressChunk(byte[] buffer, int length)
        {
            using (var ms = new MemoryStream())
            {
                if (CompressionType == CompressionType.Zlib)
                {
                    // UDZO - zlib compression
                    WriteZlibHeader(ms);
                    using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                    {
                        deflate.Write(buffer, 0, length);
                    }
                    WriteAdler32(ms, buffer, length);
                }
                else if (CompressionType == CompressionType.Bzip2)
                {
                    // UDBZ - bzip2 compression
                    using (var bzip2 = new BZip2Stream(ms, SharpCompress.Compressors.CompressionMode.Compress, true))
                    {
                        bzip2.Write(buffer, 0, length);
                    }
                }
                else
                {
                    throw new NotSupportedException($"Compression type {CompressionType} is not supported");
                }

                return ms.ToArray();
            }
        }

        private void WriteZlibHeader(Stream s)
        {
            // CMF = 0x78 (Deflate, 32k window)
            // FLG = 0xDA (Level 3, Check)
            s.WriteByte(0x78);
            s.WriteByte(0xDA);
        }

        private void WriteAdler32(Stream s, byte[] data, int length)
        {
            // Simple Adler32 implementation
            uint a = 1, b = 0;
            for (int i = 0; i < length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            uint adler = (b << 16) | a;

            // Write Big Endian
            s.WriteByte((byte)(adler >> 24));
            s.WriteByte((byte)(adler >> 16));
            s.WriteByte((byte)(adler >> 8));
            s.WriteByte((byte)adler);
        }

        private string GeneratePlist(List<BlockEntry> blocks, ulong totalSize)
        {
            // We need to generate the 'mish' block data (Base64)
            byte[] mishData = GenerateMishBlock(blocks);
            string base64Mish = Convert.ToBase64String(mishData);

            // Split base64 into lines for prettiness (optional but standard)
            var sb = new StringBuilder();
            for (int i = 0; i < base64Mish.Length; i += 68)
            {
                sb.Append(base64Mish.Substring(i, Math.Min(68, base64Mish.Length - i)));
                sb.Append("\n");
            }

            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
	<key>resource-fork</key>
	<dict>
		<key>blkx</key>
		<array>
			<dict>
				<key>Attributes</key>
				<string>0x0050</string>
				<key>Data</key>
				<data>
{sb.ToString()}				</data>
				<key>ID</key>
				<string>-1</string>
				<key>Name</key>
				<string>Driver Descriptor Map</string>
			</dict>
		</array>
		<key>plst</key>
		<array>
			<dict>
				<key>Attributes</key>
				<string>0x0050</string>
				<key>Data</key>
				<data>
				</data>
				<key>ID</key>
				<string>0</string>
				<key>Name</key>
				<string>Apple Partition Map</string>
			</dict>
		</array>
	</dict>
</dict>
</plist>";
        }

        private byte[] GenerateMishBlock(List<BlockEntry> blocks)
        {
            // 'mish' block structure
            // Signature (4) 'mish'
            // Version (4) 1
            // Start Sector (8)
            // Sector Count (8)
            // Data Offset (8)
            // Buffers Needed (4)
            // Block Descriptors (4)
            // Reserved (24)
            // Checksum (132)
            // Block Run Count (4)
            // Block Runs...

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Swap(0x6D697368)); // 'mish'
                w.Write(Swap(1)); // Version
                w.Write(Swap((ulong)0)); // Start Sector

                ulong totalSectors = 0;
                foreach (var b in blocks) if (b.Type != 0xFFFFFFFF) totalSectors += b.UncompressedLength;

                w.Write(Swap(totalSectors)); // Sector Count
                w.Write(Swap((ulong)0)); // Data Offset
                
                // Buffers Needed: Maximum sectors per block (ChunkSize / SectorSize)
                uint buffersNeeded = (uint)(ChunkSize / SectorSize);
                w.Write(Swap(buffersNeeded)); // Buffers Needed (2048 for 1MB chunks)
                
                w.Write(Swap(0)); // Block Descriptors
                w.Write(new byte[24]); // Reserved

                // Checksum (136 bytes: type(4) + size(4) + data(128))
                w.Write(Swap((uint)0)); // Checksum Type
                w.Write(Swap((uint)0)); // Checksum Size
                w.Write(new byte[128]); // Checksum Data + Padding

                w.Write(Swap(blocks.Count)); // Block Run Count (now at offset 0xC8)

                foreach (var b in blocks)
                {
                    // Block Run
                    // Type (4)
                    // Reserved (4)
                    // Sector Start (8)
                    // Sector Count (8)
                    // Comp Offset (8)
                    // Comp Length (8)

                    w.Write(Swap(b.Type));
                    w.Write(0); // Reserved
                    w.Write(Swap(b.UncompressedOffset));
                    w.Write(Swap(b.UncompressedLength));
                    w.Write(Swap(b.CompressedOffset));
                    w.Write(Swap(b.CompressedLength));
                }

                return ms.ToArray();
            }
        }

        private void WriteKoly(Stream s, KolyBlock k)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Swap(k.Signature));
                w.Write(Swap(k.Version));
                w.Write(Swap(k.HeaderSize));
                w.Write(Swap(k.Flags));
                w.Write(Swap(k.RunningDataForkOffset));
                w.Write(Swap(k.DataForkOffset));
                w.Write(Swap(k.DataForkLength));
                w.Write(Swap(k.RsrcForkOffset));
                w.Write(Swap(k.RsrcForkLength));
                w.Write(Swap(k.SegmentNumber));
                w.Write(Swap(k.SegmentCount));
                w.Write(k.SegmentId.ToByteArray()); // GUID is mixed endian?
                w.Write(Swap(k.DataForkChecksumType));
                w.Write(Swap(k.DataForkChecksumSize));
                w.Write(k.DataForkChecksum ?? new byte[128]);
                w.Write(Swap(k.XmlOffset));
                w.Write(Swap(k.XmlLength));
                w.Write(k.Reserved1 ?? new byte[120]);
                w.Write(Swap(k.ChecksumType));
                w.Write(Swap(k.ChecksumSize));
                w.Write(k.Checksum ?? new byte[128]);
                w.Write(Swap(k.ImageVariant));
                w.Write(Swap(k.SectorCount));
                w.Write(Swap(k.Reserved2));
                w.Write(Swap(k.Reserved3));
                w.Write(Swap(k.Reserved4));

                // Pad to 512 bytes
                // Calculated: 80 + 136 + 16 + 120 + 136 + 4 + 8 + 12 = 512?
                // Let's verify offsets.
                // 0-80: Header (80)
                // 80-216: DataForkChecksum (136)
                // 216-232: XML Offset/Length (16)
                // 232-352: Reserved1 (120)
                // 352-488: Checksum (136)
                // 488-492: ImageVariant (4)
                // 492-500: SectorCount (8)
                // 500-512: Reserved2/3/4 (12)

                // My code writes:
                // ...
                // Reserved1 (120)
                // Checksum (136)
                // ImageVariant (4)
                // SectorCount (8)
                // Reserved2 (4)
                // Reserved3 (4)
                // Reserved4 (4) -> Total 12.

                // So no extra padding needed if Reserved1 is 120.
                // Let's check Reserved1 write.
                // w.Write(k.Reserved1 ?? new byte[120]);

                // So we don't need w.Write(new byte[192]); anymore.

                byte[] data = ms.ToArray();
                s.Write(data, 0, data.Length);
            }
        }

        private uint Swap(uint v) => (uint)System.Net.IPAddress.HostToNetworkOrder((int)v);
        private int Swap(int v) => System.Net.IPAddress.HostToNetworkOrder(v);
        private ulong Swap(ulong v) => (ulong)System.Net.IPAddress.HostToNetworkOrder((long)v);

        private static long AlignUp(long value, int alignment)
        {
            long mask = alignment - 1;
            return (value + mask) & ~mask;
        }

        private struct BlockEntry
        {
            public uint Type;
            public ulong UncompressedOffset;
            public ulong UncompressedLength;
            public ulong CompressedOffset;
            public ulong CompressedLength;
        }
    }
}
