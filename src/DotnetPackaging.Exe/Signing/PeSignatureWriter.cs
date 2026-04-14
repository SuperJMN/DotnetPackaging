using System.Buffers.Binary;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Signing;

internal static class PeSignatureWriter
{
    private const ushort WinCertRevision2 = 0x0200;
    private const ushort WinCertTypePkcsSignedData = 0x0002;

    public static Result<byte[]> EmbedSignature(byte[] peData, byte[] pkcs7Signature)
    {
        return PeFile.Parse(peData).Map(pe => Embed(peData, pkcs7Signature, pe));
    }

    private static byte[] Embed(byte[] peData, byte[] pkcs7Signature, PeFile pe)
    {
        // WIN_CERTIFICATE: dwLength(4) + wRevision(2) + wCertificateType(2) + bCertificate(N)
        int winCertHeaderSize = 8;
        int winCertLength = winCertHeaderSize + pkcs7Signature.Length;
        int paddedLength = AlignTo8(winCertLength);

        // Place the certificate table at the end of the current file
        int certTableOffset = peData.Length;
        int totalSize = certTableOffset + paddedLength;

        var result = new byte[totalSize];
        Array.Copy(peData, result, peData.Length);

        // Write WIN_CERTIFICATE structure
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(certTableOffset), winCertLength);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(certTableOffset + 4), WinCertRevision2);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(certTableOffset + 6), WinCertTypePkcsSignedData);
        pkcs7Signature.CopyTo(result, certTableOffset + winCertHeaderSize);

        // Update Certificate Table data directory entry
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(pe.CertTableDirEntryOffset), certTableOffset);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(pe.CertTableDirEntryOffset + 4), paddedLength);

        // Recalculate PE checksum
        uint checksum = CalculatePeChecksum(result, pe.ChecksumOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(pe.ChecksumOffset), checksum);

        return result;
    }

    private static int AlignTo8(int value) => (value + 7) & ~7;

    internal static uint CalculatePeChecksum(byte[] data, int checksumOffset)
    {
        // Zero out the existing checksum field before computing
        long checksum = 0;

        for (int i = 0; i < data.Length; i += 2)
        {
            // Skip the 4-byte checksum field
            if (i >= checksumOffset && i < checksumOffset + 4)
                continue;

            ushort word = (i + 1 < data.Length)
                ? (ushort)(data[i] | (data[i + 1] << 8))
                : data[i];

            checksum += word;
            checksum = (checksum & 0xFFFF) + (checksum >> 16);
        }

        checksum = (checksum & 0xFFFF) + (checksum >> 16);
        checksum += data.Length;

        return (uint)checksum;
    }
}
