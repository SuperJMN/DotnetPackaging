using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmArchiveWriter
{
    private static readonly byte[] LeadMagic = { 0xED, 0xAB, 0xEE, 0xDB };

    public static byte[] Build(PackageMetadata metadata, RpmLayout layout)
    {
        var fileList = RpmFileListBuilder.Build(layout, metadata.ModificationTime);
        var payload = CpioArchiveWriter.Build(fileList.Entries);
        var compressedPayload = CompressGzip(payload);

        var header = RpmHeaderWriter.BuildMetadataHeader(metadata, fileList, payload.Length, compressedPayload);

        var signature = RpmHeaderWriter.BuildSignatureHeader(header, compressedPayload);
        var signaturePadded = PadToEight(signature);

        var lead = BuildLead(metadata);
        return Combine(lead, signaturePadded, header, compressedPayload);
    }

    private static byte[] BuildLead(PackageMetadata metadata)
    {
        var lead = new byte[96];
        LeadMagic.CopyTo(lead, 0);
        lead[4] = 0x04;
        lead[5] = 0x00;

        WriteInt16(lead.AsSpan(6, 2), 0);
        WriteInt16(lead.AsSpan(8, 2), 1);

        var name = $"{metadata.Package}-{metadata.Version}-1";
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var nameLength = Math.Min(nameBytes.Length, 65);
        nameBytes.AsSpan(0, nameLength).CopyTo(lead.AsSpan(10, nameLength));

        WriteInt16(lead.AsSpan(76, 2), 1);
        WriteInt16(lead.AsSpan(78, 2), 5);

        return lead;
    }

    private static void WriteInt16(Span<byte> buffer, short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
    }

    private static byte[] CompressGzip(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
            gzip.Flush();
        }

        return output.ToArray();
    }

    private static byte[] PadToEight(byte[] data)
    {
        var padding = data.Length % 8 == 0 ? 0 : 8 - (data.Length % 8);
        if (padding == 0)
        {
            return data;
        }

        var buffer = new byte[data.Length + padding];
        Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
        return buffer;
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var length = parts.Sum(part => part.Length);
        var buffer = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, buffer, offset, part.Length);
            offset += part.Length;
        }

        return buffer;
    }
}
