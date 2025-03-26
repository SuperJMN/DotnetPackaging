using System.IO.Compression;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace MsixPackaging.Core;

/// <summary>
/// Class for creating a ZIP file from pre-compressed entries.
/// Implements the bare minimum to write local headers,
/// the central directory, and the end of central directory record.
/// </summary>
public class MsixBuilder : IAsyncDisposable
{
    private readonly Stream baseStream;
    private readonly Maybe<ILogger> logger;
    private readonly List<MsixEntry> entries = new List<MsixEntry>();
    private readonly List<long> localHeaderOffsets = new List<long>();
    private bool finished = false;
    
    // Constant indicating we always use Zip64
    private const bool AlwaysUseZip64 = true;

    public MsixBuilder(Stream stream, Maybe<ILogger> logger)
    {
        this.logger = logger.Map(l => l.ForContext<MsixBuilder>());
        baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Adds a new pre-compressed entry to the ZIP.
    /// </summary>
    /// <param name="entry">The entry with all its metadata.</param>
    public async Task PutNextEntry(MsixEntry entry)
    {
        if (finished)
            throw new InvalidOperationException("ZIP has already been finalized.");

        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        // Record the current offset where the local header will be written
        long localHeaderOffset = baseStream.Position;
        localHeaderOffsets.Add(localHeaderOffset);

        WriteLocalFileHeader(entry);
        logger.Debug("Dumping data for {Entry}", entry);
        await entry.Compressed.DumpTo(baseStream);
        await WriteDataDescriptor(entry);

        //// Add the entry for the central directory
        entries.Add(entry);
    }

    private void WriteLocalFileHeader(MsixEntry entry)
    {
        using (var writer = new BinaryWriter(baseStream, Encoding.UTF8, leaveOpen: true))
        {
            // Signature
            writer.Write(0x04034b50);
            // Version
            writer.Write((short)45); // 0x002D for MSIX
            // Flags: enable Data Descriptor (bit 3)
            writer.Write((short)8); // 0x0008
            // Compression method
            short compressionMethod = (short)(entry.CompressionLevel == CompressionLevel.Optimal ? 8 : 0);
            writer.Write(compressionMethod);
            // Date/time
            int dosTime = GetDosTime(entry.ModificationTime);
            writer.Write(dosTime);
            // CRC-32, compressed size and uncompressed size: 0 (to be specified in Data Descriptor)
            writer.Write(0); // CRC-32
            writer.Write(0); // Compressed size
            writer.Write(0); // Uncompressed size
            // Name
            byte[] nameBytes = Encoding.UTF8.GetBytes(entry.FullPath);
            writer.Write((short)nameBytes.Length);
            
            // Extra field for Zip64
            writer.Write((short)0); // No extra field, but still using Zip64 features elsewhere
            
            // File name
            writer.Write(nameBytes);
        }
    }

    private async Task WriteDataDescriptor(MsixEntry entry)
    {
        using (var writer = new BinaryWriter(baseStream, Encoding.UTF8, leaveOpen: true))
        {
            // Data Descriptor signature
            writer.Write(0x08074b50);
            // CRC-32
            writer.Write(await entry.Original.Crc32());

            // IMPORTANT: Write 8 bytes for each size (Zip64 format)
            writer.Write((uint)await entry.Compressed.GetSize());
            writer.Write((uint)0); // 4 more bytes to complete 8

            writer.Write((uint)await entry.Original.GetSize());
            writer.Write((uint)0); // 4 more bytes to complete 8
        }
    }

    private async Task WriteCentralDirectory()
    {
        long centralDirStart = baseStream.Position;
        using (var writer = new BinaryWriter(baseStream, Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MsixEntry entry = entries[i];
                long localHeaderOffset = localHeaderOffsets[i];
                byte[] nameBytes = Encoding.UTF8.GetBytes(entry.FullPath);

                // Central header signature: 0x02014b50
                writer.Write(0x02014b50);
                writer.Write((short)45); // Version made by
                writer.Write((short)45); // Version needed to extract
                writer.Write((short)8);  // General purpose flag (Data Descriptor)
                short compressionMethod = (short)(entry.CompressionLevel == CompressionLevel.Optimal ? 8 : 0);
                writer.Write(compressionMethod);
                int dosTime = GetDosTime(entry.ModificationTime);
                writer.Write((int)dosTime);
                writer.Write(await entry.Original.Crc32());

                if (AlwaysUseZip64)
                {
                    writer.Write(0xFFFFFFFF); // Compressed size
                    writer.Write(0xFFFFFFFF); // Uncompressed size
                }
                else
                {
                    writer.Write((uint)await entry.Compressed.GetSize());
                    writer.Write((uint)await entry.Original.GetSize());
                }

                writer.Write((short)nameBytes.Length);
                writer.Write((short)(AlwaysUseZip64 ? 28 : 0)); // Extra field size
                writer.Write((short)0); // Comment length
                writer.Write((short)0); // Disk number
                writer.Write((short)0); // Internal attributes
                writer.Write((int)0);   // External attributes

                writer.Write(AlwaysUseZip64 ? 0xFFFFFFFF : (uint)localHeaderOffset);

                writer.Write(nameBytes);

                if (AlwaysUseZip64)
                {
                    await WriteCentralDirectoryExtraField(entry, writer, localHeaderOffset);
                }
            }

            long centralDirSize = baseStream.Position - centralDirStart;

            if (AlwaysUseZip64)
            {
                WriteZip64EndOfCentralDirectoryRecord(centralDirStart, centralDirSize, entries.Count);
                WriteZip64EndOfCentralDirectoryLocator(baseStream.Position - 56);
            }

            WriteEndOfCentralDirectory(centralDirStart, centralDirSize, entries.Count);
        }
    }

    private async Task WriteCentralDirectoryExtraField(MsixEntry entry, BinaryWriter writer, long localHeaderOffset)
    {
        // For files requiring Zip64
        ushort headerId = 0x0001; // Extra field ID for Zip64
        ushort dataSize = 24;

        writer.Write(headerId);
        writer.Write(dataSize);

        writer.Write((uint)await entry.Original.GetSize());
        writer.Write((uint)(await entry.Original.GetSize() >> 32));

        writer.Write((uint)await entry.Compressed.GetSize());
        writer.Write((uint)(await entry.Compressed.GetSize() >> 32));

        writer.Write((uint)localHeaderOffset);
        writer.Write((uint)(localHeaderOffset >> 32));
    }

    private void WriteZip64EndOfCentralDirectoryRecord(long centralDirStart, long centralDirSize, int entryCount)
    {
        using (var writer = new BinaryWriter(baseStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x06064b50); // Zip64 EOCD Record signature
            writer.Write((ulong)44);  // Record size
            writer.Write((ushort)45); // Version made by
            writer.Write((ushort)45); // Version needed to extract
            writer.Write((uint)0);    // Disk number
            writer.Write((uint)0);    // Disk with central dir start
            writer.Write((ulong)entryCount);
            writer.Write((ulong)entryCount);
            writer.Write((ulong)centralDirSize);
            writer.Write((ulong)centralDirStart);
        }
    }

    private void WriteZip64EndOfCentralDirectoryLocator(long zip64EOCDPos)
    {
        using (var writer = new BinaryWriter(baseStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x07064b50); // Zip64 EOCD Locator signature
            writer.Write((uint)0);    // Disk with EOCD
            writer.Write((ulong)zip64EOCDPos);
            writer.Write((uint)1);    // Total disks
        }
    }

    private void WriteEndOfCentralDirectory(long centralDirStart, long centralDirSize, int entryCount)
    {
        using (var writer = new BinaryWriter(baseStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x06054b50); // EOCD signature

            if (AlwaysUseZip64)
            {
                writer.Write((ushort)0xFFFF); // Disk number
                writer.Write((ushort)0xFFFF); // Disk with central dir
                writer.Write((ushort)0xFFFF); // Entries on this disk
                writer.Write((ushort)0xFFFF); // Total entries
                writer.Write(0xFFFFFFFF);     // Central dir size
                writer.Write(0xFFFFFFFF);     // Offset of start
            }
            else
            {
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)entryCount);
                writer.Write((ushort)entryCount);
                writer.Write((uint)centralDirSize);
                writer.Write((uint)centralDirStart);
            }

            writer.Write((ushort)0); // Comment length
        }
    }

    /// <summary>
    /// Finalizes the ZIP by writing the central directory.
    /// </summary>
    public async Task Finish()
    {
        if (!finished)
        {
            await WriteCentralDirectory();
            finished = true;
        }
    }

    /// <summary>
    /// Converts a DateTime to DOS format (packed into an int).
    /// </summary>
    private int GetDosTime(DateTime dt)
    {
        int dosTime = ((dt.Second / 2) & 0x1F)
                      | ((dt.Minute & 0x3F) << 5)
                      | ((dt.Hour & 0x1F) << 11);
        int dosDate = (dt.Day & 0x1F)
                      | ((dt.Month & 0x0F) << 5)
                      | (((dt.Year - 1980) & 0x7F) << 9);
        return (dosDate << 16) | dosTime;
    }

    public async ValueTask DisposeAsync()
    {
        if (!finished)
        {
            await Finish();
        }

        GC.SuppressFinalize(this);
    }
}
