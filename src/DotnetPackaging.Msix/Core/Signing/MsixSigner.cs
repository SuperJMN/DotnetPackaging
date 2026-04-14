using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Msix.Core.Signing;

/// <summary>
/// Generates AppxSignature.p7x using pure managed PKCS#7 signing.
/// The signature covers the SHA256 digest of AppxBlockMap.xml, encoded as
/// an SPC_INDIRECT_DATA_CONTENT Authenticode structure.
/// </summary>
internal static class MsixSigner
{
    private static readonly byte[] PkcxMagic = { 0x50, 0x4B, 0x43, 0x58 };

    // OID constants for Authenticode structures
    private static readonly string SpcIndirectDataOid = "1.3.6.1.4.1.311.2.1.4";
    private static readonly string SpcSipInfoOid = "1.3.6.1.4.1.311.2.1.30";
    private static readonly string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private static readonly string SpcSpOpusInfoOid = "1.3.6.1.4.1.311.2.1.12";

    // APPX SIP GUID: {F1B2A244-4C10-44B1-B246-0EF2E760A5FE} (reversed for SpcSipInfo)
    private static readonly Guid AppxSipGuid = new("F1B2A244-4C10-44B1-B246-0EF2E760A5FE");

    public static Result<byte[]> Sign(byte[] blockMapBytes, X509Certificate2 certificate)
    {
        return Result.Try(() =>
        {
            var blockMapHash = SHA256.HashData(blockMapBytes);
            var spcContent = BuildSpcIndirectDataContent(blockMapHash);
            var contentInfo = new ContentInfo(new Oid(SpcIndirectDataOid), spcContent);

            var signedCms = new SignedCms(contentInfo, false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
            {
                DigestAlgorithm = new Oid(Sha256Oid),
                IncludeOption = X509IncludeOption.WholeChain
            };

            signer.SignedAttributes.Add(BuildOpusInfoAttribute());

            signedCms.ComputeSignature(signer);
            var pkcs7 = signedCms.Encode();

            var result = new byte[PkcxMagic.Length + pkcs7.Length];
            PkcxMagic.CopyTo(result, 0);
            pkcs7.CopyTo(result, PkcxMagic.Length);
            return result;
        });
    }

    private static byte[] BuildSpcIndirectDataContent(byte[] hash)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();

        // SpcAttributeTypeAndOptionalValue
        writer.PushSequence();
        writer.WriteObjectIdentifier(SpcSipInfoOid);
        // SpcSipInfo: tagged [0] EXPLICIT
        var taggedWriter = new AsnWriter(AsnEncodingRules.DER);
        taggedWriter.PushSequence();
        taggedWriter.WriteInteger(0x00010000); // dwSipVersion
        WriteGuid(taggedWriter, AppxSipGuid);
        taggedWriter.WriteInteger(0); // reserved
        taggedWriter.WriteInteger(0);
        taggedWriter.WriteInteger(0);
        taggedWriter.WriteInteger(0);
        taggedWriter.WriteInteger(0);
        taggedWriter.PopSequence();
        writer.WriteEncodedValue(taggedWriter.Encode());
        writer.PopSequence();

        // DigestInfo
        writer.PushSequence();
        // AlgorithmIdentifier
        writer.PushSequence();
        writer.WriteObjectIdentifier(Sha256Oid);
        writer.WriteNull();
        writer.PopSequence();
        writer.WriteOctetString(hash);
        writer.PopSequence();

        writer.PopSequence();
        return writer.Encode();
    }

    private static void WriteGuid(AsnWriter writer, Guid guid)
    {
        var bytes = guid.ToByteArray();
        writer.WriteOctetString(bytes);
    }

    private static CryptographicAttributeObject BuildOpusInfoAttribute()
    {
        // Empty SpcSpOpusInfo — just an empty SEQUENCE
        var opusWriter = new AsnWriter(AsnEncodingRules.DER);
        opusWriter.PushSequence();
        opusWriter.PopSequence();

        var oid = new Oid(SpcSpOpusInfoOid);
        var collection = new AsnEncodedDataCollection(new AsnEncodedData(oid, opusWriter.Encode()));
        return new CryptographicAttributeObject(oid, collection);
    }
}
