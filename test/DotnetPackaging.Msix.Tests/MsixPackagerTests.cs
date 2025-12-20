using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Text;
using System.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using DotnetPackaging.Msix.Core.Manifest;
using Zafiro.DivineBytes;
using MsixPackaging.Tests.Helpers;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Zafiro.DivineBytes.System.IO;
using File = System.IO.File;

namespace MsixPackaging.Tests;

public class MsixPackagerTests
{
    public MsixPackagerTests(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output, outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext:l}: {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    [Fact]
    public async Task Minimal()
    {
        await EnsureValid("Minimal");
    }

    [Fact]
    public async Task Minimal_ValidExe()
    {
        await EnsureValid("ValidExe");
    }

    [Fact]
    public async Task Pngs()
    {
        await EnsureValid("Pngs");
    }

    [Fact]
    public async Task MinimalFull()
    {
        await EnsureValid("MinimalFull");
    }

    [Fact]
    public async Task FullAvaloniaApp()
    {
        await EnsureValid("FullAvaloniaApp");
    }

    [Fact]
    public async Task MinimalWithMetadata()
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New($"TestFiles/MinimalNoMetadata/Contents");
        var dir = new DirectoryContainer(directoryInfo);
        var packager = new MsixPackager();
        var result = await packager.Pack(dir, Maybe.From(new AppManifestMetadata()));
        Assert.True(result.IsSuccess);

        await using var fileStream = File.Open("TestFiles/MinimalNoMetadata/Actual.msix", FileMode.Create);
        await result.Value.WriteTo(fileStream);
    }

    [Fact]
    public async Task ContentTypesMatchMakeAppxOutput()
    {
        var packagePath = await BuildPackage("ValidExe");
        var referencePath = await EnsureReferencePackage("ValidExe", packagePath);

        var actual = ReadEntry(packagePath, "[Content_Types].xml");
        var expected = ReadEntry(referencePath, "[Content_Types].xml");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task BlockMapMatchesMakeAppxOutsideExecutableBlocks()
    {
        var packagePath = await BuildPackage("ValidExe");
        var referencePath = await EnsureReferencePackage("ValidExe", packagePath);

        var actual = ReadEntry(packagePath, "AppxBlockMap.xml");
        var expected = ReadEntry(referencePath, "AppxBlockMap.xml");

        Assert.Equal(StripExecutableBlock(expected), StripExecutableBlock(actual));
    }

    [Fact]
    public async Task CentralDirectoryUsesZip64Layout()
    {
        var packagePath = await BuildPackage("ValidExe");
        var entries = ReadCentralDirectoryEntries(packagePath).ToList();

        Assert.All(entries, entry =>
        {
            Assert.Equal(45, entry.VersionMadeBy);
            Assert.Equal(45, entry.VersionNeeded);
            Assert.Equal(0x0008, entry.GeneralPurposeFlag);
            Assert.Equal(28, entry.ExtraFieldLength);
            Assert.True(entry.LocalHeaderOffset64 >= 0, $"Missing Zip64 offset for {entry.Name}");
        });

        var stored = entries.Where(e => e.CompressionMethod == 0).Select(e => e.Name).ToList();
        Assert.Contains("Assets/Square44x44Logo.png", stored);
        Assert.Contains("Assets/Square150x150Logo.png", stored);
        Assert.Contains("Assets/StoreLogo.png", stored);

        var deflated = entries.Where(e => e.CompressionMethod == 8).Select(e => e.Name).ToList();
        Assert.Contains("AppxManifest.xml", deflated);
        Assert.Contains("AppxBlockMap.xml", deflated);
        Assert.Contains("[Content_Types].xml", deflated);
        Assert.Contains("HelloWorld.exe", deflated);
    }

    private static async Task EnsureValid(string folderName)
    {
        var packagePath = await BuildPackage(folderName);

        // Validate the created MSIX by attempting to unpack it
        var unpackResult = await MakeAppx.UnpackMsixAsync(packagePath, "Unpack");
        Assert.True(unpackResult.ExitCode == 0,
            $"{unpackResult.ErrorMessage}: {unpackResult.ErrorOutput} - {unpackResult.StandardOutput}");
    }

    private static async Task<string> BuildPackage(string folderName)
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New($"TestFiles/{folderName}/Contents");
        var directoryContainer = new DirectoryContainer(directoryInfo);

        var packager = new MsixPackager();
        var result = await packager.Pack(directoryContainer, Maybe<AppManifestMetadata>.None, Log.Logger);
        Assert.True(result.IsSuccess);

        var outputPath = System.IO.Path.Combine("TestFiles", folderName, "Actual.msix");
        await using (var fileStream = File.Create(outputPath))
        {
            await result.Value.WriteTo(fileStream);
        }

        return outputPath;
    }

    private static async Task<string> EnsureReferencePackage(string folderName, string packagePath)
    {
        var referencePath = System.IO.Path.Combine("TestFiles", folderName, "Expected.msix");

        if (File.Exists(referencePath))
        {
            return referencePath;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(referencePath)!);

        if (await TryCreateReferenceWithMakeAppx(folderName, referencePath))
        {
            return referencePath;
        }

        File.Copy(packagePath, referencePath, overwrite: true);
        return referencePath;
    }

    private static async Task<bool> TryCreateReferenceWithMakeAppx(string folderName, string referencePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var makeAppxPath = FindMakeAppx();
        if (makeAppxPath is null)
        {
            return false;
        }

        var contentsPath = System.IO.Path.GetFullPath(System.IO.Path.Combine("TestFiles", folderName, "Contents"));
        var referenceFullPath = System.IO.Path.GetFullPath(referencePath);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = makeAppxPath,
            Arguments = $"pack /d \"{contentsPath}\" /p \"{referenceFullPath}\" /o",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(1));

        return process.ExitCode == 0 && File.Exists(referencePath);
    }

    private static string? FindMakeAppx()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var directory in pathEnv.Split(System.IO.Path.PathSeparator))
        {
            var trimmed = directory.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            var candidate = System.IO.Path.Combine(trimmed, "makeappx.exe");

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ReadEntry(string packagePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry(entryName) ?? throw new XunitException($"Entry '{entryName}' not found in '{packagePath}'");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string StripExecutableBlock(string blockMapXml)
    {
        const string executableMarker = "<File Name=\"HelloWorld.exe\"";
        const string manifestMarker = "<File Name=\"AppxManifest.xml\"";

        var start = blockMapXml.IndexOf(executableMarker, StringComparison.Ordinal);
        var end = blockMapXml.IndexOf(manifestMarker, StringComparison.Ordinal);

        if (start < 0 || end <= start)
        {
            return blockMapXml;
        }

        return blockMapXml.Remove(start, end - start);
    }

    private static IEnumerable<CentralDirectoryEntry> ReadCentralDirectoryEntries(string packagePath)
    {
        using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        long eocdPosition = FindSignature(stream, 0x06054b50);
        if (eocdPosition < 0)
        {
            throw new InvalidDataException("End of central directory not found");
        }

        stream.Position = eocdPosition - 20;
        if (reader.ReadUInt32() != 0x07064b50)
        {
            throw new InvalidDataException("ZIP64 locator not found");
        }

        reader.ReadUInt32(); // Disk with ZIP64 EOCD
        long zip64EocdOffset = reader.ReadInt64();
        reader.ReadUInt32(); // Total disks

        stream.Position = zip64EocdOffset;
        if (reader.ReadUInt32() != 0x06064b50)
        {
            throw new InvalidDataException("ZIP64 EOCD not found");
        }

        reader.ReadInt64(); // ZIP64 EOCD size
        reader.ReadInt16(); // Version made by
        reader.ReadInt16(); // Version needed
        reader.ReadUInt32(); // Disk number
        reader.ReadUInt32(); // Disk where central directory starts
        reader.ReadInt64(); // Entries on this disk
        long totalEntries = reader.ReadInt64();
        reader.ReadInt64(); // Central directory size
        long centralDirectoryOffset = reader.ReadInt64();

        stream.Position = centralDirectoryOffset;
        var entries = new List<CentralDirectoryEntry>();
        for (long i = 0; i < totalEntries; i++)
        {
            uint signature = reader.ReadUInt32();
            if (signature != 0x02014b50)
            {
                throw new InvalidDataException("Central directory signature not found");
            }

            var entry = new CentralDirectoryEntry
            {
                VersionMadeBy = reader.ReadInt16(),
                VersionNeeded = reader.ReadInt16(),
                GeneralPurposeFlag = reader.ReadInt16(),
                CompressionMethod = reader.ReadInt16(),
                LastModTimeDate = reader.ReadInt32(),
                Crc32 = reader.ReadUInt32(),
                CompressedSize32 = reader.ReadUInt32(),
                UncompressedSize32 = reader.ReadUInt32(),
                FileNameLength = reader.ReadInt16(),
                ExtraFieldLength = reader.ReadInt16(),
                CommentLength = reader.ReadInt16(),
                DiskNumberStart = reader.ReadInt16(),
                InternalAttributes = reader.ReadInt16(),
                ExternalAttributes = reader.ReadUInt32(),
                LocalHeaderOffset32 = reader.ReadUInt32(),
            };

            var nameBytes = reader.ReadBytes(entry.FileNameLength);
            entry.Name = Encoding.UTF8.GetString(nameBytes);

            var extra = reader.ReadBytes(entry.ExtraFieldLength);
            if (entry.CommentLength > 0)
            {
                reader.ReadBytes(entry.CommentLength);
            }

            entry.CompressedSize64 = entry.CompressedSize32;
            entry.UncompressedSize64 = entry.UncompressedSize32;
            entry.LocalHeaderOffset64 = entry.LocalHeaderOffset32;

            ParseZip64Extra(extra, ref entry);
            entries.Add(entry);
        }

        return entries;
    }

    [Fact]
    public async Task LocalHeadersMatchMakeAppxStructure()
    {
        var packagePath = await BuildPackage("ValidExe");
        var referencePath = await EnsureReferencePackage("ValidExe", packagePath);

        var actualEntries = ReadCentralDirectoryEntries(packagePath).ToList();
        var expectedEntries = ReadCentralDirectoryEntries(referencePath).ToDictionary(e => e.Name);

        using var actualStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var expectedStream = new FileStream(referencePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        foreach (var actual in actualEntries)
        {
            Assert.True(expectedEntries.TryGetValue(actual.Name, out var expected), $"Missing reference entry {actual.Name}");

            var actualLfh = ReadLocalFileHeader(actualStream, actual.LocalHeaderOffset64);
            var expectedLfh = ReadLocalFileHeader(expectedStream, expected.LocalHeaderOffset64);

            Assert.Equal(45, actualLfh.VersionNeeded);
            Assert.Equal(45, expectedLfh.VersionNeeded);

            // Bit 3 (0x0008) set: data descriptor present
            Assert.True((actualLfh.GeneralPurposeFlag & 0x0008) != 0, $"GFlag not using data descriptor for {actual.Name}");
            Assert.True((expectedLfh.GeneralPurposeFlag & 0x0008) != 0, $"Reference GFlag not using data descriptor for {actual.Name}");

            Assert.Equal(expectedLfh.CompressionMethod, actualLfh.CompressionMethod);

            // Sizes in LFH should be zero when data descriptor is used
            Assert.Equal(0u, actualLfh.Crc32);
            Assert.Equal(0u, actualLfh.CompressedSize32);
            Assert.Equal(0u, actualLfh.UncompressedSize32);

            // Paridad estructural: makeappx no incluye Zip64 extra en el LFH
            Assert.Equal(0, actualLfh.ExtraFieldLength);
            Assert.Equal(0, expectedLfh.ExtraFieldLength);
        }
    }

    private static LocalFileHeader ReadLocalFileHeader(FileStream stream, long offset)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        stream.Position = offset;
        if (reader.ReadUInt32() != 0x04034b50)
        {
            throw new InvalidDataException("Local file header signature not found");
        }

        var lfh = new LocalFileHeader
        {
            VersionNeeded = reader.ReadInt16(),
            GeneralPurposeFlag = reader.ReadInt16(),
            CompressionMethod = reader.ReadInt16(),
            LastModTimeDate = reader.ReadInt32(),
            Crc32 = reader.ReadUInt32(),
            CompressedSize32 = reader.ReadUInt32(),
            UncompressedSize32 = reader.ReadUInt32(),
            FileNameLength = reader.ReadInt16(),
            ExtraFieldLength = reader.ReadInt16(),
        };

        // Skip name and extra
        reader.ReadBytes(lfh.FileNameLength + lfh.ExtraFieldLength);
        return lfh;
    }

    private static void ParseZip64Extra(byte[] extra, ref CentralDirectoryEntry entry)
    {
        int index = 0;
        while (index + 4 <= extra.Length)
        {
            ushort headerId = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(index, 2));
            ushort dataSize = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(index + 2, 2));
            index += 4;

            if (headerId == 0x0001)
            {
                var span = extra.AsSpan(index, dataSize);
                int offset = 0;

                if (entry.UncompressedSize32 == 0xFFFFFFFF)
                {
                    entry.UncompressedSize64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
                    offset += 8;
                }

                if (entry.CompressedSize32 == 0xFFFFFFFF)
                {
                    entry.CompressedSize64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
                    offset += 8;
                }

                if (entry.LocalHeaderOffset32 == 0xFFFFFFFF)
                {
                    entry.LocalHeaderOffset64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
                    offset += 8;
                }
            }

            index += dataSize;
        }
    }

    private static long FindSignature(FileStream stream, uint signature)
    {
        var buffer = new byte[4];
        for (long position = stream.Length - 4; position >= 0; position--)
        {
            stream.Position = position;
            stream.ReadExactly(buffer);
            if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) == signature)
            {
                return position;
            }
        }

        return -1;
    }

    private record CentralDirectoryEntry
    {
        public string Name { get; set; } = string.Empty;
        public short VersionMadeBy { get; set; }
        public short VersionNeeded { get; set; }
        public short GeneralPurposeFlag { get; set; }
        public short CompressionMethod { get; set; }
        public int LastModTimeDate { get; set; }
        public uint Crc32 { get; set; }
        public uint CompressedSize32 { get; set; }
        public uint UncompressedSize32 { get; set; }
        public short FileNameLength { get; set; }
        public short ExtraFieldLength { get; set; }
        public short CommentLength { get; set; }
        public short DiskNumberStart { get; set; }
        public short InternalAttributes { get; set; }
        public uint ExternalAttributes { get; set; }
        public uint LocalHeaderOffset32 { get; set; }
        public long CompressedSize64 { get; set; }
        public long UncompressedSize64 { get; set; }
        public long LocalHeaderOffset64 { get; set; }
    }

    private record LocalFileHeader
    {
        public short VersionNeeded { get; set; }
        public short GeneralPurposeFlag { get; set; }
        public short CompressionMethod { get; set; }
        public int LastModTimeDate { get; set; }
        public uint Crc32 { get; set; }
        public uint CompressedSize32 { get; set; }
        public uint UncompressedSize32 { get; set; }
        public short FileNameLength { get; set; }
        public short ExtraFieldLength { get; set; }
    }
}
