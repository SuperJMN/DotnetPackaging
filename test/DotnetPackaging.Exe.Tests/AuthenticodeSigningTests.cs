using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotnetPackaging.Exe.Signing;
using FluentAssertions;

namespace DotnetPackaging.Exe.Tests;

public class AuthenticodeSigningTests
{
    private static byte[] CreateMinimalPe()
    {
        // Build a minimal PE32+ (x64) that is valid enough for our parser and signer.
        // DOS header (64 bytes) + PE signature (4) + COFF header (20) + Optional header (240) = 328 bytes
        var pe = new byte[512];

        // DOS header
        pe[0] = 0x4D; pe[1] = 0x5A; // MZ
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(0x3C), 64); // e_lfanew → PE starts at 64

        // PE signature
        pe[64] = 0x50; pe[65] = 0x45; pe[66] = 0x00; pe[67] = 0x00;

        // COFF header (20 bytes at offset 68)
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(68), 0x8664); // Machine: AMD64
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(70), 1);      // NumberOfSections: 1
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(84), 240);    // SizeOfOptionalHeader

        // Optional header (starts at offset 88)
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(88), 0x20B); // Magic: PE32+
        // Checksum is at optional header offset 64 → absolute 88+64 = 152
        // Cert table is at data dir offset 112 + 4*8 = 144 → absolute 88+144 = 232
        // NumberOfRvaAndSizes at offset 108 → absolute 88+108 = 196
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(196), 16); // NumberOfRvaAndSizes

        // Section header (at offset 88+240 = 328, 40 bytes)
        // .text section
        pe[328] = (byte)'.'; pe[329] = (byte)'t'; pe[330] = (byte)'e'; pe[331] = (byte)'x'; pe[332] = (byte)'t';
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(328 + 8), 128);  // VirtualSize
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(328 + 12), 0x1000); // VirtualAddress
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(328 + 16), 128); // SizeOfRawData
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(328 + 20), 368); // PointerToRawData

        // Fill section data with some bytes
        for (int i = 368; i < 496; i++) pe[i] = (byte)(i & 0xFF);

        return pe;
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Test Code Signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.3") }, false));
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    [Fact]
    public void PeFile_should_detect_valid_pe()
    {
        var pe = CreateMinimalPe();
        PeFile.IsPeFile(pe).Should().BeTrue();
    }

    [Fact]
    public void PeFile_should_reject_non_pe()
    {
        PeFile.IsPeFile(new byte[] { 0x00, 0x01, 0x02, 0x03 }).Should().BeFalse();
        PeFile.IsPeFile(Array.Empty<byte>()).Should().BeFalse();
    }

    [Fact]
    public void PeFile_should_parse_minimal_pe()
    {
        var pe = CreateMinimalPe();
        var result = PeFile.Parse(pe);
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPe32Plus.Should().BeTrue();
        result.Value.ChecksumOffset.Should().Be(88 + 64); // optional header + 64
        result.Value.CertTableDirEntryOffset.Should().Be(88 + 112 + 4 * 8); // optional header + 112 + 32
    }

    [Fact]
    public void PeFile_should_compute_authenticode_hash()
    {
        var pe = CreateMinimalPe();
        var parsed = PeFile.Parse(pe);
        parsed.IsSuccess.Should().BeTrue();

        var hash = parsed.Value.ComputeAuthenticodeHash(pe);
        hash.Should().HaveCount(32); // SHA-256
        hash.Should().NotBeEquivalentTo(new byte[32]); // not all zeros
    }

    [Fact]
    public void AuthenticodeSigner_should_create_pkcs7_signature()
    {
        var hash = SHA256.HashData(new byte[] { 1, 2, 3, 4 });
        using var cert = CreateTestCertificate();

        var result = AuthenticodeSigner.CreateSignature(hash, cert);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        // PKCS#7 signatures start with SEQUENCE tag (0x30)
        result.Value[0].Should().Be(0x30);
    }

    [Fact]
    public void PeSignatureWriter_should_embed_signature_in_pe()
    {
        var pe = CreateMinimalPe();
        var fakeSignature = new byte[256];
        Random.Shared.NextBytes(fakeSignature);

        var result = PeSignatureWriter.EmbedSignature(pe, fakeSignature);
        result.IsSuccess.Should().BeTrue();

        var signed = result.Value;
        signed.Length.Should().BeGreaterThan(pe.Length);

        // Certificate table directory entry should be updated
        var parsed = PeFile.Parse(signed);
        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.CertTableDataOffset.Should().Be(pe.Length); // cert appended at end
        parsed.Value.CertTableDataSize.Should().BeGreaterThan(0);

        // WIN_CERTIFICATE header: dwLength (4) + wRevision (2) + wCertificateType (2)
        int certOffset = pe.Length;
        var winCertLength = BinaryPrimitives.ReadInt32LittleEndian(signed.AsSpan(certOffset));
        winCertLength.Should().Be(8 + fakeSignature.Length);
        var revision = BinaryPrimitives.ReadUInt16LittleEndian(signed.AsSpan(certOffset + 4));
        revision.Should().Be(0x0200);
        var certType = BinaryPrimitives.ReadUInt16LittleEndian(signed.AsSpan(certOffset + 6));
        certType.Should().Be(0x0002);

        // PE checksum should be non-zero
        var checksum = BinaryPrimitives.ReadUInt32LittleEndian(signed.AsSpan(parsed.Value.ChecksumOffset));
        checksum.Should().NotBe(0);
    }

    [Fact]
    public void PeSigner_should_sign_pe_end_to_end()
    {
        var pe = CreateMinimalPe();
        using var cert = CreateTestCertificate();

        var result = PeSigner.Sign(pe, cert);
        result.IsSuccess.Should().BeTrue();

        var signed = result.Value;
        signed.Length.Should().BeGreaterThan(pe.Length);

        // Should still be a valid PE
        PeFile.IsPeFile(signed).Should().BeTrue();

        // Certificate table should be populated
        var parsed = PeFile.Parse(signed);
        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.CertTableDataOffset.Should().BeGreaterThan(0);
        parsed.Value.CertTableDataSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PeSigner_SignIfPe_should_pass_through_non_pe_data()
    {
        using var cert = CreateTestCertificate();
        var notPe = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        var result = PeSigner.SignIfPe(notPe, cert);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(notPe);
    }

    [Fact]
    public void PeChecksum_should_be_deterministic()
    {
        var pe = CreateMinimalPe();
        int checksumOffset = 88 + 64; // known from our minimal PE

        var checksum1 = PeSignatureWriter.CalculatePeChecksum(pe, checksumOffset);
        var checksum2 = PeSignatureWriter.CalculatePeChecksum(pe, checksumOffset);

        checksum1.Should().Be(checksum2);
        checksum1.Should().NotBe(0);
    }
}
