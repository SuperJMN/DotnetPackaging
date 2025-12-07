using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using DotnetPackaging.Formats.Dmg.Udif;
using SharpCompress.Compressors.BZip2;

namespace DotnetPackaging.Formats.Dmg.Tests
{
    public class UdifTests
    {
        [Fact]
        public void KolyBlock_IsGeneratedCorrectly()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[1024 * 1024]; // 1MB
            new Random().NextBytes(inputData);

            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);

            byte[] dmg = output.ToArray();

            // Koly block is last 512 bytes
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);

            // Check signature 'koly' (0x6B6F6C79)
            // It's Big Endian in file.
            Assert.Equal(0x6B, kolyBytes[0]);
            Assert.Equal(0x6F, kolyBytes[1]);
            Assert.Equal(0x6C, kolyBytes[2]);
            Assert.Equal(0x79, kolyBytes[3]);

            // Check version (4)
            Assert.Equal(0, kolyBytes[4]);
            Assert.Equal(0, kolyBytes[5]);
            Assert.Equal(0, kolyBytes[6]);
            Assert.Equal(4, kolyBytes[7]);
        }

        [Fact]
        public void Plist_Contains_Blkx()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(new byte[100]);
            using var output = new MemoryStream();

            writer.Create(input, output);

            byte[] dmg = output.ToArray();
            string content = Encoding.UTF8.GetString(dmg);

            Assert.Contains("<key>blkx</key>", content);
            Assert.Contains("<key>resource-fork</key>", content);
        }

        [Fact]
        public void Koly_Flags_BitZero_IsSet()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(new byte[1024]);
            using var output = new MemoryStream();
            
            writer.Create(input, output);
            
            byte[] dmg = output.ToArray();
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);
            
            uint flags = ReadBigEndianUInt32(kolyBytes, 12);
            Assert.Equal(1u, flags & 1); // Bit 0 should be set
        }

        [Fact]
        public void Mish_BuffersNeeded_IsCalculated()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[5 * 1024 * 1024]; // 5 MB
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();
            
            writer.Create(input, output);
            
            // Extract mish block from plist
            byte[] dmg = output.ToArray();
            byte[] mishData = ExtractMishBlock(dmg);
            
            uint buffersNeeded = ReadBigEndianUInt32(mishData, 0x20);
            Assert.True(buffersNeeded > 0, "Buffers needed should be calculated");
        }

        private uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | 
                   ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private uint ComputeCrc32(byte[] data, long offset, long length)
        {
            var crc = new Crc32();
            crc.Append(data.AsSpan((int)offset, (int)length));
            var hash = crc.GetCurrentHash();
            return BinaryPrimitives.ReadUInt32BigEndian(hash);
        }

        private ulong ReadBigEndianUInt64(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) | 
                   ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                   ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) | 
                   ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private byte[] ExtractMishBlock(byte[] dmg)
        {
            // Read Koly block to get XML offset/length
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);
            
            ulong xmlOffset = ReadBigEndianUInt64(kolyBytes, 0xD8);
            ulong xmlLength = ReadBigEndianUInt64(kolyBytes, 0xE0);
            
            // Extract plist
            byte[] plistBytes = new byte[xmlLength];
            Array.Copy(dmg, (long)xmlOffset, plistBytes, 0, (long)xmlLength);
            string plistText = Encoding.UTF8.GetString(plistBytes);
            
            // Find <data> tag after <key>Data</key>
            int dataKeyIndex = plistText.IndexOf("<key>Data</key>");
            if (dataKeyIndex < 0) throw new Exception("No Data key found");
            
            int dataTagStart = plistText.IndexOf("<data>", dataKeyIndex);
            int dataTagEnd = plistText.IndexOf("</data>", dataTagStart);
            
            string base64Data = plistText.Substring(dataTagStart + 6, dataTagEnd - dataTagStart - 6)
                .Replace("\n", "").Replace("\t", "").Replace(" ", "");
            
            return Convert.FromBase64String(base64Data);
        }

        [Fact]
        public void UdifWriter_SupportsBzip2Compression()
        {
            var writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
            byte[] inputData = new byte[1024 * 1024]; // 1 MB
            for (int i = 0; i < inputData.Length; i++) inputData[i] = (byte)(i % 256);

            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();
            
            writer.Create(input, output);
            
            // Extract mish and verify compression type is 0x80000006
            byte[] dmg = output.ToArray();
            byte[] mishData = ExtractMishBlock(dmg);
            
            // First block descriptor starts at 0xCC (after block run count at 0xC8)
            // Block descriptor format: Type (4 bytes) + Reserved (4 bytes) + ...
            uint compressionType = ReadBigEndianUInt32(mishData, 0xCC);
            Assert.Equal(0x80000006u, compressionType);
        }

        [Fact]
        public void Bzip2_ProducesSmallerOutput_ThanZlib()
        {
            byte[] inputData = new byte[5 * 1024 * 1024]; // 5 MB of compressible data
            // Create somewhat compressible data (repeating pattern)
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)((i / 1024) % 256);
            }
            
            var zlibWriter = new UdifWriter { CompressionType = CompressionType.Zlib };
            using var zlibOutput = new MemoryStream();
            zlibWriter.Create(new MemoryStream(inputData), zlibOutput);
            
            var bzip2Writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
            using var bzip2Output = new MemoryStream();
            bzip2Writer.Create(new MemoryStream(inputData), bzip2Output);
            
            Assert.True(bzip2Output.Length < zlibOutput.Length, 
                        $"Bzip2 ({bzip2Output.Length}) should produce smaller output than zlib ({zlibOutput.Length})");
        }

        [Fact]
        public void GeneratedDmg_WritesCrc32Checksums()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("checksum-test"));
            using var output = new MemoryStream();

            writer.Create(input, output);
            byte[] dmg = output.ToArray();

            var validator = new UdifValidator();
            var res = validator.Validate(dmg);
            Assert.True(res.IsValid);
            var koly = res.KolyBlock!.Value;

            Assert.Equal(2u, koly.DataForkChecksumType);
            Assert.Equal(4u, koly.DataForkChecksumSize);
            Assert.Equal(2u, koly.ChecksumType);
            Assert.Equal(4u, koly.ChecksumSize);

            uint expectedData = ComputeCrc32(dmg, 0, (long)koly.DataForkLength);
            uint actualData = ReadBigEndianUInt32(koly.DataForkChecksum, 0);
            Assert.Equal(expectedData, actualData);

            ulong masterLength = koly.XmlOffset + koly.XmlLength;
            uint expectedMaster = ComputeCrc32(dmg, 0, (long)masterLength);
            uint actualMaster = ReadBigEndianUInt32(koly.Checksum, 0);
            Assert.Equal(expectedMaster, actualMaster);
        }

        [Fact]
        public void Validator_DetectsChecksumMismatch()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("checksum-mismatch"));
            using var output = new MemoryStream();

            writer.Create(input, output);
            byte[] dmg = output.ToArray();

            // Corrupt data fork
            dmg[0] ^= 0xFF;
            var validator = new UdifValidator();
            var res = validator.Validate(dmg);

            Assert.False(res.IsValid);
            Assert.Contains(res.Errors, e => e.Contains("DataFork") && e.Contains("checksum"));
        }

        [Fact]
        public void Validator_DetectsMasterChecksumMismatch_OnXml()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("checksum-xml"));
            using var output = new MemoryStream();

            writer.Create(input, output);
            byte[] dmg = output.ToArray();

            // Corrupt XML region (leave data fork intact)
            var koly = new UdifValidator().Validate(dmg).KolyBlock!.Value;
            int xmlStart = (int)koly.XmlOffset;
            dmg[xmlStart] ^= 0xAA;

            var validator = new UdifValidator();
            var res = validator.Validate(dmg);

            Assert.False(res.IsValid);
            Assert.Contains(res.Errors, e => e.Contains("master", StringComparison.OrdinalIgnoreCase) && e.Contains("checksum", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GeneratedDmg_PassesValidation()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[1024 * 1024]; // 1 MB
            new Random(42).NextBytes(inputData);

            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);

            // Validate the generated DMG
            var validator = new UdifValidator();
            var result = validator.Validate(output.ToArray());

            // Print errors for diagnostics
            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"ERROR: {error}");
                }
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"WARNING: {warning}");
                }
            }

            Assert.True(result.IsValid, $"DMG validation failed with {result.Errors.Count} error(s)");
        }

        [Fact]
        public void GeneratedDmg_HasCorrectXmlOffset()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[512 * 100]; // 50KB (exact multiple of sector size)
            
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);
            
            byte[] dmg = output.ToArray();
            var validator = new UdifValidator();
            var result = validator.Validate(dmg);
            
            Assert.NotNull(result.KolyBlock);
            var koly = result.KolyBlock.Value;
            
            // XmlOffset should equal DataForkLength
            Assert.Equal(koly.DataForkLength, koly.XmlOffset);
        }

        [Fact]
        public void GeneratedDmg_HasCorrectDataForkLength()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[1024 * 1024]; // 1 MB
            
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);
            
            byte[] dmg = output.ToArray();
            var validator = new UdifValidator();
            var result = validator.Validate(dmg);
            
            Assert.NotNull(result.KolyBlock);
            var koly = result.KolyBlock.Value;
            
            // DataForkLength + XmlLength + KolySize should equal file size
            ulong expectedFileSize = koly.DataForkLength + koly.XmlLength + 512;
            Assert.Equal((ulong)dmg.Length, expectedFileSize);
        }

        [Fact]
        public void GeneratedDmg_BlocksAreAligned()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[2 * 1024 * 1024]; // 2 MB
            
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);
            
            var validator = new UdifValidator();
            var result = validator.Validate(output.ToArray());
            
            Assert.NotNull(result.BlockDescriptors);
            
            foreach (var block in result.BlockDescriptors)
            {
                if (block.Type != 0xFFFFFFFF) // Skip terminator
                {
                    Assert.True(block.CompressedOffset % 512 == 0, 
                        $"Block at offset {block.CompressedOffset} is not aligned to 512 bytes");
                }
            }
        }

        [Fact]
        public void GeneratedDmg_BlocksDoNotExceedFileSize()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[5 * 1024 * 1024]; // 5 MB
            
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);
            
            byte[] dmg = output.ToArray();
            var validator = new UdifValidator();
            var result = validator.Validate(dmg);
            
            Assert.NotNull(result.BlockDescriptors);
            
            foreach (var block in result.BlockDescriptors)
            {
                if (block.Type != 0xFFFFFFFF)
                {
                    Assert.True(block.CompressedOffset + block.CompressedLength <= (ulong)dmg.Length,
                        $"Block extends beyond file size: offset {block.CompressedOffset}, length {block.CompressedLength}, file size {dmg.Length}");
                }
            }
        }

        [Fact]
        public void Validator_RejectsMalformedDmg_WrongSignature()
        {
            byte[] malformed = new byte[512];
            // Write wrong signature
            malformed[0] = 0x42; // Not 'koly'
            malformed[1] = 0x41;
            malformed[2] = 0x44;
            malformed[3] = 0x21;
            
            var validator = new UdifValidator();
            var result = validator.Validate(malformed);
            
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("signature"));
        }

        [Fact]
        public void Validator_RejectsMalformedDmg_XmlBeyondFileSize()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[1024];
            
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();
            writer.Create(input, output);
            
            byte[] dmg = output.ToArray();
            
            // Corrupt XmlOffset to point beyond file
            int xmlOffsetPos = dmg.Length - 512 + 0xD8;
            ulong corruptOffset = (ulong)dmg.Length * 2;
            dmg[xmlOffsetPos] = (byte)(corruptOffset >> 56);
            dmg[xmlOffsetPos + 1] = (byte)(corruptOffset >> 48);
            dmg[xmlOffsetPos + 2] = (byte)(corruptOffset >> 40);
            dmg[xmlOffsetPos + 3] = (byte)(corruptOffset >> 32);
            dmg[xmlOffsetPos + 4] = (byte)(corruptOffset >> 24);
            dmg[xmlOffsetPos + 5] = (byte)(corruptOffset >> 16);
            dmg[xmlOffsetPos + 6] = (byte)(corruptOffset >> 8);
            dmg[xmlOffsetPos + 7] = (byte)corruptOffset;
            
            var validator = new UdifValidator();
            var result = validator.Validate(dmg);
            
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("XML") && e.Contains("beyond"));
        }

        [Fact]
        public void Validator_RejectsHugeBlkxSegmentSizes()
        {
            var writer = new UdifWriter();
            using var output = new MemoryStream();
            writer.Create(new MemoryStream(new byte[4096]), output);
            var dmg = output.ToArray();

            // Read koly to get XML offset/length
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);
            ulong xmlOffset = ReadBigEndianUInt64(kolyBytes, 0xD8);
            ulong xmlLength = ReadBigEndianUInt64(kolyBytes, 0xE0);

            // Decode plist and mish, then corrupt first block's CompressedLength to 85 GB
            byte[] plistBytes = dmg[(int)xmlOffset..((int)(xmlOffset + xmlLength))];
            string plistText = Encoding.UTF8.GetString(plistBytes);

            int dataKeyIndex = plistText.IndexOf("<key>Data</key>");
            int dataTagStart = plistText.IndexOf("<data>", dataKeyIndex);
            int dataTagEnd = plistText.IndexOf("</data>", dataTagStart);
            string base64Data = plistText.Substring(dataTagStart + 6, dataTagEnd - dataTagStart - 6)
                .Replace("\n", "").Replace("\t", "").Replace(" ", "").Replace("\r", "");
            var mish = Convert.FromBase64String(base64Data);

            // First block desc at 0xCC, CompressedLength at +32 (8 bytes BE)
            ulong huge = 85UL * 1024 * 1024 * 1024; // 85 GB
            mish[0xCC + 32] = (byte)(huge >> 56);
            mish[0xCC + 33] = (byte)(huge >> 48);
            mish[0xCC + 34] = (byte)(huge >> 40);
            mish[0xCC + 35] = (byte)(huge >> 32);
            mish[0xCC + 36] = (byte)(huge >> 24);
            mish[0xCC + 37] = (byte)(huge >> 16);
            mish[0xCC + 38] = (byte)(huge >> 8);
            mish[0xCC + 39] = (byte)huge;

            // Re-embed corrupted mish into plist
            string newBase64 = Convert.ToBase64String(mish);
            var sb = new StringBuilder();
            for (int i = 0; i < newBase64.Length; i += 68)
                sb.AppendLine(newBase64.Substring(i, Math.Min(68, newBase64.Length - i)));
            string newPlist = plistText.Substring(0, dataTagStart + 6) + "\n" + sb + "\t\t\t\t" + plistText.Substring(dataTagEnd);
            byte[] newPlistBytes = Encoding.UTF8.GetBytes(newPlist);

            // Splice plist back into dmg
            Array.Copy(newPlistBytes, 0, dmg, (int)xmlOffset, newPlistBytes.Length);
            // Adjust koly XmlLength to match, if changed
            if ((ulong)newPlistBytes.Length != xmlLength)
            {
                int xmlLenPos = dmg.Length - 512 + 0xE0;
                ulong nl = (ulong)newPlistBytes.Length;
                dmg[xmlLenPos] = (byte)(nl >> 56);
                dmg[xmlLenPos + 1] = (byte)(nl >> 48);
                dmg[xmlLenPos + 2] = (byte)(nl >> 40);
                dmg[xmlLenPos + 3] = (byte)(nl >> 32);
                dmg[xmlLenPos + 4] = (byte)(nl >> 24);
                dmg[xmlLenPos + 5] = (byte)(nl >> 16);
                dmg[xmlLenPos + 6] = (byte)(nl >> 8);
                dmg[xmlLenPos + 7] = (byte)nl;
            }

            var validator = new UdifValidator();
            var res = validator.Validate(dmg);
            Assert.False(res.IsValid);
            Assert.Contains(res.Errors, e => e.Contains("unreasonably large") || e.Contains("would require allocating") || e.Contains("extends beyond file size"));
        }

        [Theory]
        [InlineData(CompressionType.Zlib)]
        [InlineData(CompressionType.Bzip2)]
        public void GeneratedDmg_RoundTripsOriginalData(CompressionType compression)
        {
            var writer = new UdifWriter { CompressionType = compression };
            byte[] payload = Enumerable.Range(0, 64 * 1024).Select(i => (byte)(i % 251)).ToArray();

            using var input = new MemoryStream(payload);
            using var output = new MemoryStream();
            writer.Create(input, output);

            byte[] dmg = output.ToArray();
            var validator = new UdifValidator();
            var validation = validator.Validate(dmg);
            Assert.True(validation.IsValid, $"Validation failed: {string.Join(", ", validation.Errors)}");
            Assert.NotNull(validation.BlockDescriptors);
            Assert.NotNull(validation.KolyBlock);

            var reconstructed = new MemoryStream();
            foreach (var block in validation.BlockDescriptors!.Where(b => b.Type != 0xFFFFFFFF))
            {
                var compressed = dmg.AsSpan((int)block.CompressedOffset, (int)block.CompressedLength).ToArray();
                byte[] chunk = DecompressBlock(compressed, block.Type);

                ulong expectedLength = block.SectorCount * 512;
                Assert.True((ulong)chunk.Length <= expectedLength, $"Chunk larger than expected: {chunk.Length} > {expectedLength}");

                reconstructed.Write(chunk, 0, chunk.Length);
                ulong padding = expectedLength - (ulong)chunk.Length;
                if (padding > 0)
                {
                    reconstructed.Write(new byte[padding], 0, (int)padding);
                }
            }

            var rebuilt = reconstructed.ToArray();
            Assert.True(rebuilt.Length >= payload.Length);
            Assert.Equal(payload, rebuilt[..payload.Length]);
            Assert.Equal(validation.KolyBlock!.Value.SectorCount * 512, (ulong)rebuilt.Length);
        }

        [Fact]
        public void TerminatorBlock_PointsToXmlStart()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(new byte[4096]);
            using var output = new MemoryStream();

            writer.Create(input, output);

            var validator = new UdifValidator();
            var result = validator.Validate(output.ToArray());

            Assert.NotNull(result.KolyBlock);
            Assert.NotNull(result.BlockDescriptors);

            var terminator = Assert.Single(result.BlockDescriptors!.Where(b => b.Type == 0xFFFFFFFF));
            Assert.Equal(result.KolyBlock!.Value.XmlOffset, terminator.CompressedOffset);
        }

        [Fact]
        public void GeneratedDmg_IsReadableByDmgwiz()
        {
            const string toolPath = "/usr/bin/dmgwiz";
            if (!File.Exists(toolPath))
            {
                return; // Tool not available in this environment
            }

            string referenceDmg = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../EvaluacionesApp.Desktop.dmg"));
            if (File.Exists(referenceDmg))
            {
                if (!TryRunDmgwiz(toolPath, referenceDmg, out var refExit, out var refStdout, out var refStderr))
                {
                    Console.WriteLine($"Skipping dmgwiz validation because reference DMG could not be parsed (exit {refExit}). Out: {refStdout} Err: {refStderr}");
                    return;
                }
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"udif-{Guid.NewGuid():N}.dmg");
            bool keepFile = false;

            try
            {
                var writer = new UdifWriter();
                byte[] payload = new byte[128 * 1024];
                new Random(99).NextBytes(payload);

                using (var input = new MemoryStream(payload))
                using (var output = File.Create(tempFile))
                {
                    writer.Create(input, output);
                }

                if (!TryRunDmgwiz(toolPath, tempFile, out var exitCode, out var stdout, out var stderr))
                {
                    keepFile = true;
                    Assert.True(false, $"dmgwiz exited with {exitCode}. Out: {stdout} Err: {stderr}. DMG: {tempFile}");
                }
            }
            finally
            {
                if (!keepFile && File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
                else if (keepFile)
                {
                    Console.WriteLine($"DMG kept for inspection: {tempFile}");
                }
            }
        }

        private static bool TryRunDmgwiz(string toolPath, string dmgPath, out int exitCode, out string stdout, out string stderr)
        {
            var psi = new ProcessStartInfo(toolPath, $"info {dmgPath}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                exitCode = -1;
                stdout = string.Empty;
                stderr = "Failed to start dmgwiz process";
                return false;
            }

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(true); } catch { /* best effort */ }
                exitCode = -1;
                stdout = proc.StandardOutput.ReadToEnd();
                stderr = proc.StandardError.ReadToEnd() + " (timeout)";
                return false;
            }

            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            exitCode = proc.ExitCode;
            return exitCode == 0;
        }

        private static byte[] DecompressBlock(byte[] data, uint type)
        {
            using var input = new MemoryStream(data);
            Stream decompressor = type switch
            {
                0x80000005 => new ZLibStream(input, CompressionMode.Decompress, true),
                0x80000006 => new BZip2Stream(input, SharpCompress.Compressors.CompressionMode.Decompress, true),
                _ => throw new InvalidOperationException($"Unsupported block type 0x{type:X}")
            };

            using var output = new MemoryStream();
            using (decompressor)
            {
                decompressor.CopyTo(output);
            }
            return output.ToArray();
        }
    }
}
