using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Signing;

public static class CertificateProvider
{
    public static Result<X509Certificate2> Get(Maybe<string> pfxPath, Maybe<string> pfxPassword, string publisherCN)
    {
        return pfxPath.HasValue
            ? LoadFromPfx(pfxPath.Value, pfxPassword.GetValueOrDefault(string.Empty))
            : GenerateSelfSigned(publisherCN);
    }

    public static Result<X509Certificate2> LoadFromPfx(string path, string password)
    {
        return Result.Try(() => X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.Exportable));
    }

    private static Result<X509Certificate2> GenerateSelfSigned(string subjectName)
    {
        return Result.Try(() =>
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new("1.3.6.1.5.5.7.3.3") }, // Code Signing
                    false));

            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            var notAfter = DateTimeOffset.UtcNow.AddYears(1);
            var cert = request.CreateSelfSigned(notBefore, notAfter);

            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        });
    }
}
