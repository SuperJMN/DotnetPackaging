using System.Buffers.Binary;
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
        await Msix.FromDirectoryAndMetadata(dir, new AppManifestMetadata(), Maybe<ILogger>.None)
            .Map(async source =>
            {
                await using var fileStream = File.Open("TestFiles/MinimalNoMetadata/Actual.msix", FileMode.Create);
                return await source.WriteTo(fileStream);
            });
    }

    [Fact]
    public async Task ContentTypesMatchMakeAppxOutput()
    {
        var packagePath = await BuildPackage("ValidExe");
        var referencePath = System.IO.Path.Combine("TestFiles", "ValidExe", "Expected.msix");

        var actual = ReadEntry(packagePath, "[Content_Types].xml");
        var expected = ReadEntry(referencePath, "[Content_Types].xml");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task BlockMapMatchesMakeAppxOutsideExecutableBlocks()
    {
        var packagePath = await BuildPackage("ValidExe");
        var referencePath = System.IO.Path.Combine("TestFiles", "ValidExe", "Expected.msix");

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

        var result = Msix.FromDirectory(directoryContainer, Log.Logger.AsMaybe());

        if (result.IsFailure)
        {
            Assert.True(false, $"Failed to create MSIX package: {result.Error}");
        }

        var outputPath = System.IO.Path.Combine("TestFiles", folderName, "Actual.msix");
        await using (var fileStream = File.Create(outputPath))
        {
            await result.Value.WriteTo(fileStream);
        }

        return outputPath;
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
}
