using System.Buffers.Binary;
using System.Security.Cryptography;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Signing;

internal sealed class PeFile
{
    public int PeHeaderOffset { get; }
    public bool IsPe32Plus { get; }
    public int ChecksumOffset { get; }
    public int CertTableDirEntryOffset { get; }
    public int CertTableDataOffset { get; }
    public int CertTableDataSize { get; }

    private PeFile(int peHeaderOffset, bool isPe32Plus, int checksumOffset,
        int certTableDirEntryOffset, int certTableDataOffset, int certTableDataSize)
    {
        PeHeaderOffset = peHeaderOffset;
        IsPe32Plus = isPe32Plus;
        ChecksumOffset = checksumOffset;
        CertTableDirEntryOffset = certTableDirEntryOffset;
        CertTableDataOffset = certTableDataOffset;
        CertTableDataSize = certTableDataSize;
    }

    public static Result<PeFile> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 64)
            return Result.Failure<PeFile>("File too small to be a valid PE");

        if (data[0] != 0x4D || data[1] != 0x5A)
            return Result.Failure<PeFile>("Not a valid PE file: missing MZ signature");

        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3C));
        if (peOffset < 0 || peOffset + 24 > data.Length)
            return Result.Failure<PeFile>("Invalid PE header offset");

        if (data[peOffset] != 0x50 || data[peOffset + 1] != 0x45 ||
            data[peOffset + 2] != 0x00 || data[peOffset + 3] != 0x00)
            return Result.Failure<PeFile>("Not a valid PE file: missing PE\\0\\0 signature");

        int optionalHeaderOffset = peOffset + 24;
        if (optionalHeaderOffset + 2 > data.Length)
            return Result.Failure<PeFile>("Invalid Optional Header");

        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(optionalHeaderOffset));
        bool isPe32Plus = magic == 0x20B;

        if (magic != 0x10B && magic != 0x20B)
            return Result.Failure<PeFile>($"Unsupported PE format (magic=0x{magic:X4})");

        int checksumOffset = optionalHeaderOffset + 64;

        // Data directory offset differs between PE32 and PE32+
        int dataDirectoryOffset = optionalHeaderOffset + (isPe32Plus ? 112 : 96);

        // Certificate Table is the 5th data directory entry (index 4)
        int certTableDirEntryOffset = dataDirectoryOffset + 4 * 8;

        if (certTableDirEntryOffset + 8 > data.Length)
            return Result.Failure<PeFile>("PE file too small: missing Certificate Table directory entry");

        int certTableDataOffset = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(certTableDirEntryOffset));
        int certTableDataSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(certTableDirEntryOffset + 4));

        return new PeFile(peOffset, isPe32Plus, checksumOffset,
            certTableDirEntryOffset, certTableDataOffset, certTableDataSize);
    }

    public static bool IsPeFile(ReadOnlySpan<byte> data)
    {
        if (data.Length < 64) return false;
        if (data[0] != 0x4D || data[1] != 0x5A) return false;
        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3C));
        if (peOffset < 0 || peOffset + 4 > data.Length) return false;
        return data[peOffset] == 0x50 && data[peOffset + 1] == 0x45 &&
               data[peOffset + 2] == 0x00 && data[peOffset + 3] == 0x00;
    }

    public byte[] ComputeAuthenticodeHash(ReadOnlySpan<byte> data)
    {
        // Authenticode hash covers the entire file except:
        // - The PE checksum field (4 bytes)
        // - The Certificate Table directory entry (8 bytes)
        // - Existing certificate data (from CertTableDataOffset to end)
        int fileEnd = CertTableDataOffset > 0 ? CertTableDataOffset : data.Length;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // Region 1: [0, ChecksumOffset)
        hash.AppendData(data.Slice(0, ChecksumOffset));

        // Region 2: [ChecksumOffset + 4, CertTableDirEntryOffset)
        hash.AppendData(data.Slice(ChecksumOffset + 4, CertTableDirEntryOffset - (ChecksumOffset + 4)));

        // Region 3: [CertTableDirEntryOffset + 8, fileEnd)
        int region3Start = CertTableDirEntryOffset + 8;
        if (fileEnd > region3Start)
            hash.AppendData(data.Slice(region3Start, fileEnd - region3Start));

        return hash.GetHashAndReset();
    }
}
