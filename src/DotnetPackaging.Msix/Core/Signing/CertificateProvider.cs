using CSharpFunctionalExtensions;

namespace DotnetPackaging.Msix.Core.Signing;

// Delegates to the shared implementation in DotnetPackaging.Signing
internal static class CertificateProvider
{
    public static Result<System.Security.Cryptography.X509Certificates.X509Certificate2> Get(
        Maybe<string> pfxPath, Maybe<string> pfxPassword, string publisherCN)
        => DotnetPackaging.Signing.CertificateProvider.Get(pfxPath, pfxPassword, publisherCN);
}
