using System.Buffers.Binary;
using System.Text;
using System.Xml;

namespace DotnetPackaging.Dmg.Inspector;

internal class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: DmgInspector <path-to-dmg>");
            Console.WriteLine("       DmgInspector <path-to-dmg> --compare <reference-dmg>");
            return;
        }

        var targetDmg = args[0];
        string? referenceDmg = null;

        if (args.Length >= 3 && args[1] == "--compare")
        {
            referenceDmg = args[2];
        }

        Console.WriteLine($"=== Inspecting DMG: {targetDmg} ===\n");
        var targetInfo = InspectDmg(targetDmg);

        if (referenceDmg != null)
        {
            Console.WriteLine($"\n=== Inspecting Reference DMG: {referenceDmg} ===\n");
            var refInfo = InspectDmg(referenceDmg);

            Console.WriteLine("\n=== COMPARISON ===\n");
            CompareDmgs(targetInfo, refInfo);
        }
    }

    static DmgInfo InspectDmg(string path)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            Console.WriteLine($"ERROR: File not found: {path}");
            Environment.Exit(1);
        }

        Console.WriteLine($"File size: {fileInfo.Length:N0} bytes");

        using var stream = File.OpenRead(path);

        // Read last 512 bytes (koly block)
        if (stream.Length < 512)
        {
            Console.WriteLine("ERROR: File too small to contain koly block (< 512 bytes)");
            Environment.Exit(1);
        }

        stream.Seek(-512, SeekOrigin.End);
        var kolyBytes = new byte[512];
        stream.ReadExactly(kolyBytes);

        var dmgInfo = new DmgInfo
        {
            FilePath = path,
            FileSize = fileInfo.Length,
            KolyBlock = ParseKolyBlock(kolyBytes, fileInfo.Length)
        };

        // Read and parse XML plist
        if (dmgInfo.KolyBlock.IsValid)
        {
            dmgInfo.XmlPlist = ReadXmlPlist(stream, dmgInfo.KolyBlock);
            dmgInfo.BlkxEntries = ParseBlkxEntries(dmgInfo.XmlPlist);
        }

        return dmgInfo;
    }

    static KolyBlock ParseKolyBlock(byte[] bytes, long fileSize)
    {
        Console.WriteLine("\n--- KOLY BLOCK (512-byte footer) ---");

        var koly = new KolyBlock();

        // Signature (offset 0, 4 bytes)
        var signature = Encoding.ASCII.GetString(bytes, 0, 4);
        Console.WriteLine($"Signature: '{signature}' (expected 'koly')");
        koly.IsValid = signature == "koly";

        if (!koly.IsValid)
        {
            Console.WriteLine("ERROR: Invalid signature! Not a UDIF DMG.");
            return koly;
        }

        // Version (offset 4, 4 bytes, big endian)
        koly.Version = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4));
        Console.WriteLine($"Version: {koly.Version} (expected 4)");

        // HeaderSize (offset 8, 4 bytes, big endian)
        koly.HeaderSize = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4));
        Console.WriteLine($"HeaderSize: {koly.HeaderSize} (expected 512)");

        if (koly.HeaderSize != 512)
        {
            Console.WriteLine($"ERROR: Invalid HeaderSize! Expected 512, got {koly.HeaderSize}");
            koly.IsValid = false;
        }

        // Flags (offset 12, 4 bytes, big endian)
        koly.Flags = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(12, 4));
        Console.WriteLine($"Flags: 0x{koly.Flags:X8}");

        // RunningDataForkOffset (offset 16, 8 bytes, big endian)
        koly.RunningDataForkOffset = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(16, 8));
        Console.WriteLine($"RunningDataForkOffset: {koly.RunningDataForkOffset}");

        // DataForkOffset (offset 24, 8 bytes, big endian)
        koly.DataForkOffset = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(24, 8));
        Console.WriteLine($"DataForkOffset: {koly.DataForkOffset} (usually 0)");

        // DataForkLength (offset 32, 8 bytes, big endian)
        koly.DataForkLength = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(32, 8));
        Console.WriteLine($"DataForkLength: {koly.DataForkLength:N0} bytes");

        // Validate DataFork range
        var dataForkEnd = koly.DataForkOffset + koly.DataForkLength;
        Console.WriteLine($"DataFork range: {koly.DataForkOffset} - {dataForkEnd}");
        if (dataForkEnd > (ulong)fileSize)
        {
            Console.WriteLine($"ERROR: DataFork extends beyond file size! End={dataForkEnd}, FileSize={fileSize}");
            koly.IsValid = false;
        }

        // RsrcForkOffset (offset 40, 8 bytes, big endian)
        koly.RsrcForkOffset = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(40, 8));
        Console.WriteLine($"RsrcForkOffset: {koly.RsrcForkOffset}");

        // RsrcForkLength (offset 48, 8 bytes, big endian)
        koly.RsrcForkLength = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(48, 8));
        Console.WriteLine($"RsrcForkLength: {koly.RsrcForkLength}");

        // SegmentNumber (offset 56, 4 bytes, big endian)
        koly.SegmentNumber = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(56, 4));
        Console.WriteLine($"SegmentNumber: {koly.SegmentNumber}");

        // SegmentCount (offset 60, 4 bytes, big endian)
        koly.SegmentCount = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(60, 4));
        Console.WriteLine($"SegmentCount: {koly.SegmentCount}");

        // XMLOffset (offset 216, 8 bytes, big endian)
        koly.XmlOffset = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(216, 8));
        Console.WriteLine($"XMLOffset: {koly.XmlOffset}");

        // XMLLength (offset 224, 8 bytes, big endian)
        koly.XmlLength = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(224, 8));
        Console.WriteLine($"XMLLength: {koly.XmlLength:N0} bytes");

        // Validate XML range
        var xmlEnd = koly.XmlOffset + koly.XmlLength;
        Console.WriteLine($"XML range: {koly.XmlOffset} - {xmlEnd}");
        if (xmlEnd > (ulong)fileSize)
        {
            Console.WriteLine($"ERROR: XML extends beyond file size! End={xmlEnd}, FileSize={fileSize}");
            koly.IsValid = false;
        }

        // Expected position of XML (should be right before koly block)
        var expectedXmlEnd = (ulong)fileSize - 512;
        if (xmlEnd != expectedXmlEnd)
        {
            Console.WriteLine($"WARNING: XML does not end at koly block. Expected end={expectedXmlEnd}, actual={xmlEnd}");
        }

        // SectorCount (offset 492, 8 bytes, big endian)
        koly.SectorCount = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(492, 8));
        Console.WriteLine($"SectorCount: {koly.SectorCount} sectors");
        Console.WriteLine($"SectorCount in bytes: {koly.SectorCount * 512:N0} bytes");

        // Validate SectorCount calculation
        var calculatedSectorCount = (koly.DataForkLength + 511) / 512;
        Console.WriteLine($"Calculated sector count: {calculatedSectorCount}");
        if (koly.SectorCount != calculatedSectorCount)
        {
            Console.WriteLine($"WARNING: SectorCount mismatch! Expected {calculatedSectorCount}, got {koly.SectorCount}");
        }

        Console.WriteLine($"\nKOLY block valid: {koly.IsValid}");

        return koly;
    }

    static string? ReadXmlPlist(Stream stream, KolyBlock koly)
    {
        if (!koly.IsValid || koly.XmlLength == 0)
            return null;

        Console.WriteLine("\n--- XML PLIST ---");

        try
        {
            stream.Seek((long)koly.XmlOffset, SeekOrigin.Begin);
            var xmlBytes = new byte[koly.XmlLength];
            stream.ReadExactly(xmlBytes);

            var xmlContent = Encoding.UTF8.GetString(xmlBytes);
            Console.WriteLine($"XML content length: {xmlContent.Length} characters");

            // Try to parse as XML
            var doc = new XmlDocument();
            doc.LoadXml(xmlContent);
            Console.WriteLine("XML is well-formed ✓");

            return xmlContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR parsing XML: {ex.Message}");
            return null;
        }
    }

    static List<BlkxEntry> ParseBlkxEntries(string? xmlContent)
    {
        var entries = new List<BlkxEntry>();

        if (string.IsNullOrEmpty(xmlContent))
            return entries;

        Console.WriteLine("\n--- BLKX ENTRIES (Block Tables) ---");

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlContent);

            // Navigate to resource-fork/blkx array
            var root = doc.DocumentElement;
            var dict = root?.SelectSingleNode("dict");
            var resourceFork = dict?.SelectSingleNode("dict[preceding-sibling::key[1][text()='resource-fork']]");
            var blkxArray = resourceFork?.SelectSingleNode("array[preceding-sibling::key[1][text()='blkx']]");

            if (blkxArray == null)
            {
                Console.WriteLine("No blkx array found in XML");
                return entries;
            }

            var blkxDicts = blkxArray.SelectNodes("dict");
            Console.WriteLine($"Found {blkxDicts?.Count ?? 0} blkx entries");

            if (blkxDicts == null)
                return entries;

            int index = 0;
            foreach (XmlNode blkxDict in blkxDicts)
            {
                var entry = ParseBlkxEntry(blkxDict, index++);
                if (entry != null)
                    entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR parsing BLKX entries: {ex.Message}");
        }

        return entries;
    }

    static BlkxEntry? ParseBlkxEntry(XmlNode blkxDict, int index)
    {
        try
        {
            var name = blkxDict.SelectSingleNode("string[preceding-sibling::key[1][text()='Name']]")?.InnerText ?? $"Entry {index}";
            var dataNode = blkxDict.SelectSingleNode("data[preceding-sibling::key[1][text()='Data']]");

            if (dataNode == null)
                return null;

            var base64Data = dataNode.InnerText.Replace("\n", "").Replace("\r", "").Replace("\t", "").Trim();
            var mishBytes = Convert.FromBase64String(base64Data);

            Console.WriteLine($"\n[{index}] {name}");
            Console.WriteLine($"  Base64 length: {base64Data.Length}");
            Console.WriteLine($"  Decoded length: {mishBytes.Length} bytes");

            var entry = ParseMishBlock(mishBytes);
            entry.Name = name;
            entry.Index = index;

            return entry;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR parsing blkx entry {index}: {ex.Message}");
            return null;
        }
    }

    static BlkxEntry ParseMishBlock(byte[] mishBytes)
    {
        var entry = new BlkxEntry();

        if (mishBytes.Length < 204)
        {
            Console.WriteLine($"  ERROR: mish block too small ({mishBytes.Length} bytes, expected >= 204)");
            return entry;
        }

        // Signature (offset 0, 4 bytes)
        var signature = Encoding.ASCII.GetString(mishBytes, 0, 4);
        Console.WriteLine($"  Signature: '{signature}' (expected 'mish')");

        if (signature != "mish")
        {
            Console.WriteLine($"  ERROR: Invalid mish signature!");
            return entry;
        }

        // Version (offset 4, 4 bytes, big endian)
        entry.Version = BinaryPrimitives.ReadUInt32BigEndian(mishBytes.AsSpan(4, 4));
        Console.WriteLine($"  Version: {entry.Version}");

        // SectorNumber (offset 8, 8 bytes, big endian)
        entry.SectorNumber = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(8, 8));
        Console.WriteLine($"  SectorNumber: {entry.SectorNumber}");

        // SectorCount (offset 16, 8 bytes, big endian)
        entry.SectorCount = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(16, 8));
        Console.WriteLine($"  SectorCount: {entry.SectorCount}");

        // DataOffset (offset 24, 8 bytes, big endian)
        entry.DataOffset = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(24, 8));
        Console.WriteLine($"  DataOffset: {entry.DataOffset}");

        // BuffersNeeded (offset 32, 4 bytes, big endian)
        entry.BuffersNeeded = BinaryPrimitives.ReadUInt32BigEndian(mishBytes.AsSpan(32, 4));
        Console.WriteLine($"  BuffersNeeded: {entry.BuffersNeeded}");

        // BlockDescriptors (offset 36, 4 bytes, big endian)
        entry.BlockDescriptors = BinaryPrimitives.ReadUInt32BigEndian(mishBytes.AsSpan(36, 4));
        Console.WriteLine($"  BlockDescriptors: {entry.BlockDescriptors}");

        // NumberOfBlockChunks (offset 200, 4 bytes, big endian)
        entry.NumberOfBlockChunks = BinaryPrimitives.ReadUInt32BigEndian(mishBytes.AsSpan(200, 4));
        Console.WriteLine($"  NumberOfBlockChunks: {entry.NumberOfBlockChunks}");

        // Parse block chunks (starting at offset 204)
        var offset = 204;
        for (uint i = 0; i < entry.NumberOfBlockChunks && offset + 40 <= mishBytes.Length; i++)
        {
            var chunk = new BlockChunk
            {
                EntryType = BinaryPrimitives.ReadUInt32BigEndian(mishBytes.AsSpan(offset, 4)),
                Comment = BinaryPrimitives.ReadUInt32BigEndian(mishBytes.AsSpan(offset + 4, 4)),
                SectorNumber = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(offset + 8, 8)),
                SectorCount = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(offset + 16, 8)),
                CompressedOffset = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(offset + 24, 8)),
                CompressedLength = BinaryPrimitives.ReadUInt64BigEndian(mishBytes.AsSpan(offset + 32, 8))
            };

            entry.Chunks.Add(chunk);

            Console.WriteLine($"    Chunk {i}: Type=0x{chunk.EntryType:X8}, Sector={chunk.SectorNumber}, Count={chunk.SectorCount}, Offset={chunk.CompressedOffset}, Length={chunk.CompressedLength}");

            offset += 40;
        }

        return entry;
    }

    static void CompareDmgs(DmgInfo target, DmgInfo reference)
    {
        Console.WriteLine("Comparing KOLY blocks...");

        CompareField("Version", target.KolyBlock.Version, reference.KolyBlock.Version);
        CompareField("HeaderSize", target.KolyBlock.HeaderSize, reference.KolyBlock.HeaderSize);
        CompareField("Flags", $"0x{target.KolyBlock.Flags:X8}", $"0x{reference.KolyBlock.Flags:X8}");
        CompareField("DataForkOffset", target.KolyBlock.DataForkOffset, reference.KolyBlock.DataForkOffset);
        CompareField("DataForkLength", target.KolyBlock.DataForkLength, reference.KolyBlock.DataForkLength);
        CompareField("XmlOffset", target.KolyBlock.XmlOffset, reference.KolyBlock.XmlOffset);
        CompareField("XmlLength", target.KolyBlock.XmlLength, reference.KolyBlock.XmlLength);
        CompareField("SectorCount", target.KolyBlock.SectorCount, reference.KolyBlock.SectorCount);

        Console.WriteLine($"\nBLKX entry count: Target={target.BlkxEntries.Count}, Reference={reference.BlkxEntries.Count}");

        var minCount = Math.Min(target.BlkxEntries.Count, reference.BlkxEntries.Count);
        for (int i = 0; i < minCount; i++)
        {
            Console.WriteLine($"\nComparing BLKX entry {i}:");
            Console.WriteLine($"  Target: {target.BlkxEntries[i].Name}");
            Console.WriteLine($"  Reference: {reference.BlkxEntries[i].Name}");
            CompareField("  SectorNumber", target.BlkxEntries[i].SectorNumber, reference.BlkxEntries[i].SectorNumber);
            CompareField("  SectorCount", target.BlkxEntries[i].SectorCount, reference.BlkxEntries[i].SectorCount);
            CompareField("  NumberOfBlockChunks", target.BlkxEntries[i].NumberOfBlockChunks, reference.BlkxEntries[i].NumberOfBlockChunks);
        }
    }

    static void CompareField<T>(string fieldName, T targetValue, T refValue)
    {
        var match = EqualityComparer<T>.Default.Equals(targetValue, refValue);
        var marker = match ? "✓" : "✗";
        Console.WriteLine($"{marker} {fieldName}: Target={targetValue}, Reference={refValue}");
    }
}

class DmgInfo
{
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public KolyBlock KolyBlock { get; set; } = new();
    public string? XmlPlist { get; set; }
    public List<BlkxEntry> BlkxEntries { get; set; } = new();
}

class KolyBlock
{
    public bool IsValid { get; set; }
    public uint Version { get; set; }
    public uint HeaderSize { get; set; }
    public uint Flags { get; set; }
    public ulong RunningDataForkOffset { get; set; }
    public ulong DataForkOffset { get; set; }
    public ulong DataForkLength { get; set; }
    public ulong RsrcForkOffset { get; set; }
    public ulong RsrcForkLength { get; set; }
    public uint SegmentNumber { get; set; }
    public uint SegmentCount { get; set; }
    public ulong XmlOffset { get; set; }
    public ulong XmlLength { get; set; }
    public ulong SectorCount { get; set; }
}

class BlkxEntry
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public uint Version { get; set; }
    public ulong SectorNumber { get; set; }
    public ulong SectorCount { get; set; }
    public ulong DataOffset { get; set; }
    public uint BuffersNeeded { get; set; }
    public uint BlockDescriptors { get; set; }
    public uint NumberOfBlockChunks { get; set; }
    public List<BlockChunk> Chunks { get; set; } = new();
}

class BlockChunk
{
    public uint EntryType { get; set; }
    public uint Comment { get; set; }
    public ulong SectorNumber { get; set; }
    public ulong SectorCount { get; set; }
    public ulong CompressedOffset { get; set; }
    public ulong CompressedLength { get; set; }
}
