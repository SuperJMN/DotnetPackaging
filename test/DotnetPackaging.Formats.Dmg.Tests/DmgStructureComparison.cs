using Xunit.Abstractions;

namespace DotnetPackaging.Formats.Dmg.Tests
{
    /// <summary>
    /// Tests to verify structural compatibility between generated and reference DMGs
    /// </summary>
    public class DmgStructureComparison
    {
        private readonly ITestOutputHelper output;

        public DmgStructureComparison(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void CompareDmgStructure()
        {
            string generatedDmg = "/tmp/EvaluacionesApp.Test.dmg";
            string referenceDmg = "/mnt/fast/Repos/ProyectoAna/EvaluacionesApp.Desktop/bin/packages/macOS/EvaluacionesApp.Desktop.arm64.1.0.0.dmg";

            if (!File.Exists(generatedDmg))
            {
                output.WriteLine($"Generated DMG not found: {generatedDmg}. Skipping test.");
                return;
            }

            if (!File.Exists(referenceDmg))
            {
                output.WriteLine($"Reference DMG not found: {referenceDmg}. Skipping test.");
                return;
            }

            var generatedInfo = AnalyzeDmgStructure(generatedDmg);
            var referenceInfo = AnalyzeDmgStructure(referenceDmg);

            output.WriteLine("\n=== Generated DMG ===");
            PrintDmgInfo(generatedInfo);

            output.WriteLine("\n=== Reference DMG ===");
            PrintDmgInfo(referenceInfo);

            output.WriteLine("\n=== Differences ===");
            CompareStructures(generatedInfo, referenceInfo);
        }

        private DmgInfo AnalyzeDmgStructure(string path)
        {
            var info = new DmgInfo { FilePath = path };

            using (var fs = File.OpenRead(path))
            {
                info.FileSize = fs.Length;

                // Read Koly block
                fs.Seek(-512, SeekOrigin.End);
                byte[] kolyBytes = new byte[512];
                fs.Read(kolyBytes, 0, 512);

                info.KolySignature = ReadBigEndianUInt32(kolyBytes, 0);
                info.KolyVersion = ReadBigEndianUInt32(kolyBytes, 4);
                info.KolyFlags = ReadBigEndianUInt32(kolyBytes, 12);
                info.XmlOffset = ReadBigEndianUInt64(kolyBytes, 0xD8);
                info.XmlLength = ReadBigEndianUInt64(kolyBytes, 0xE0);
                info.DataLength = ReadBigEndianUInt64(kolyBytes, 0x20);
                info.SectorCount = ReadBigEndianUInt64(kolyBytes, 0xEC);

                // Read plist
                fs.Seek((long)info.XmlOffset, SeekOrigin.Begin);
                byte[] plistBytes = new byte[info.XmlLength];
                fs.Read(plistBytes, 0, (int)info.XmlLength);

                // Parse plist to find blkx
                // For simplicity, we'll just search for key markers
                string plistText = System.Text.Encoding.UTF8.GetString(plistBytes);
                info.HasBlkx = plistText.Contains("<key>blkx</key>");
                info.HasCSum = plistText.Contains("<key>cSum</key>");
                info.HasNsiz = plistText.Contains("<key>nsiz</key>");

                // Extract first blkx Data if present
                if (info.HasBlkx)
                {
                    // This is a simplified extraction - in production we'd use a proper XML parser
                    var dataStart = plistText.IndexOf("<key>Data</key>");
                    if (dataStart > 0)
                    {
                        var dataTagStart = plistText.IndexOf("<data>", dataStart);
                        var dataTagEnd = plistText.IndexOf("</data>", dataTagStart);
                        if (dataTagStart > 0 && dataTagEnd > 0)
                        {
                            var base64Data = plistText.Substring(dataTagStart + 6, dataTagEnd - dataTagStart - 6).Trim();
                            var mishBytes = Convert.FromBase64String(base64Data);
                            
                            info.MishBuffersNeeded = ReadBigEndianUInt32(mishBytes, 0x20);
                            info.MishBlockDescriptors = ReadBigEndianUInt32(mishBytes, 0x24);
                            info.MishBlockRunCount = ReadBigEndianUInt32(mishBytes, 0xC4); // Offset 196

                            // Try to detect compression type from first block descriptor
                            if (mishBytes.Length > 0xCC + 4)
                            {
                                info.CompressionType = ReadBigEndianUInt32(mishBytes, 0xCC);
                            }
                        }
                    }
                }
            }

            return info;
        }

        private void PrintDmgInfo(DmgInfo info)
        {
            output.WriteLine($"File: {Path.GetFileName(info.FilePath)}");
            output.WriteLine($"Size: {info.FileSize / (1024 * 1024)} MB");
            output.WriteLine($"Koly Signature: 0x{info.KolySignature:X8}");
            output.WriteLine($"Koly Version: {info.KolyVersion}");
            output.WriteLine($"Koly Flags: 0x{info.KolyFlags:X}");
            output.WriteLine($"XML Length: {info.XmlLength} bytes");
            output.WriteLine($"Data Length: {info.DataLength} bytes");
            output.WriteLine($"Sector Count: {info.SectorCount}");
            output.WriteLine($"Has blkx: {info.HasBlkx}");
            output.WriteLine($"Has cSum: {info.HasCSum}");
            output.WriteLine($"Has nsiz: {info.HasNsiz}");
            output.WriteLine($"Mish Buffers Needed: {info.MishBuffersNeeded}");
            output.WriteLine($"Mish Block Descriptors: {info.MishBlockDescriptors}");
            output.WriteLine($"Mish Block Run Count: {info.MishBlockRunCount}");
            output.WriteLine($"Compression Type: 0x{info.CompressionType:X8}");
        }

        private void CompareStructures(DmgInfo generated, DmgInfo reference)
        {
            if (generated.KolyFlags != reference.KolyFlags)
                output.WriteLine($"⚠ Koly Flags differ: {generated.KolyFlags:X} vs {reference.KolyFlags:X}");

            if (generated.CompressionType != reference.CompressionType)
                output.WriteLine($"⚠ Compression Type differs: 0x{generated.CompressionType:X8} vs 0x{reference.CompressionType:X8}");

            if (generated.HasCSum != reference.HasCSum)
                output.WriteLine($"⚠ cSum presence differs: {generated.HasCSum} vs {reference.HasCSum}");

            if (generated.HasNsiz != reference.HasNsiz)
                output.WriteLine($"⚠ nsiz presence differs: {generated.HasNsiz} vs {reference.HasNsiz}");

            if (generated.MishBlockDescriptors != reference.MishBlockDescriptors)
                output.WriteLine($"⚠ Mish Block Descriptors differ: {generated.MishBlockDescriptors} vs {reference.MishBlockDescriptors}");
        }

        private uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private ulong ReadBigEndianUInt64(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) | ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                   ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) | ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private class DmgInfo
        {
            public string FilePath { get; set; }
            public long FileSize { get; set; }
            public uint KolySignature { get; set; }
            public uint KolyVersion { get; set; }
            public uint KolyFlags { get; set; }
            public ulong XmlOffset { get; set; }
            public ulong XmlLength { get; set; }
            public ulong DataLength { get; set; }
            public ulong SectorCount { get; set; }
            public bool HasBlkx { get; set; }
            public bool HasCSum { get; set; }
            public bool HasNsiz { get; set; }
            public uint MishBuffersNeeded { get; set; }
            public uint MishBlockDescriptors { get; set; }
            public uint MishBlockRunCount { get; set; }
            public uint CompressionType { get; set; }
        }
    }
}
