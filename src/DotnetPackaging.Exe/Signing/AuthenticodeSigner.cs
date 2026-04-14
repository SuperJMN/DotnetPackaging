using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Signing;

internal static class AuthenticodeSigner
{
    private const string SpcIndirectDataOid = "1.3.6.1.4.1.311.2.1.4";
    private const string SpcPeImageDataOid = "1.3.6.1.4.1.311.2.1.15";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string SpcSpOpusInfoOid = "1.3.6.1.4.1.311.2.1.12";

    public static Result<byte[]> CreateSignature(byte[] authenticodeHash, X509Certificate2 certificate)
    {
        return Result.Try(() =>
        {
            var spcContent = BuildSpcIndirectDataContent(authenticodeHash);
            var contentInfo = new ContentInfo(new Oid(SpcIndirectDataOid), spcContent);

            var signedCms = new SignedCms(contentInfo, false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
            {
                DigestAlgorithm = new Oid(Sha256Oid),
                IncludeOption = X509IncludeOption.WholeChain
            };

            signer.SignedAttributes.Add(BuildOpusInfoAttribute());

            signedCms.ComputeSignature(signer);
            return signedCms.Encode();
        });
    }

    private static byte[] BuildSpcIndirectDataContent(byte[] hash)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();

        // SpcAttributeTypeAndOptionalValue
        writer.PushSequence();
        writer.WriteObjectIdentifier(SpcPeImageDataOid);

        // SpcPeImageData
        writer.PushSequence();
        // flags: BIT STRING with includeResources bit set
        writer.WriteBitString(new byte[] { 0x80 }, 6);
        // file: [0] EXPLICIT SpcLink
        var ctxExplicit0 = new Asn1Tag(TagClass.ContextSpecific, 0, true);
        writer.PushSequence(ctxExplicit0);
        // SpcLink choice [2] EXPLICIT = file
        var ctxExplicit2 = new Asn1Tag(TagClass.ContextSpecific, 2, true);
        writer.PushSequence(ctxExplicit2);
        // SpcString choice [0] IMPLICIT BMPString (empty)
        writer.WriteEncodedValue(new byte[] { 0x80, 0x00 });
        writer.PopSequence(ctxExplicit2);
        writer.PopSequence(ctxExplicit0);
        writer.PopSequence(); // SpcPeImageData

        writer.PopSequence(); // SpcAttributeTypeAndOptionalValue

        // DigestInfo
        writer.PushSequence();
        writer.PushSequence();
        writer.WriteObjectIdentifier(Sha256Oid);
        writer.WriteNull();
        writer.PopSequence();
        writer.WriteOctetString(hash);
        writer.PopSequence();

        writer.PopSequence();
        return writer.Encode();
    }

    private static CryptographicAttributeObject BuildOpusInfoAttribute()
    {
        var opusWriter = new AsnWriter(AsnEncodingRules.DER);
        opusWriter.PushSequence();
        opusWriter.PopSequence();

        var oid = new Oid(SpcSpOpusInfoOid);
        var collection = new AsnEncodedDataCollection(new AsnEncodedData(oid, opusWriter.Encode()));
        return new CryptographicAttributeObject(oid, collection);
    }
}
