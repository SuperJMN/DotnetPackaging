using System.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Signing;

internal static class PeSigner
{
    public static Result<byte[]> Sign(byte[] peBytes, X509Certificate2 certificate)
    {
        return PeFile.Parse(peBytes)
            .Map(pe => pe.ComputeAuthenticodeHash(peBytes))
            .Bind(hash => AuthenticodeSigner.CreateSignature(hash, certificate))
            .Bind(signature => PeSignatureWriter.EmbedSignature(peBytes, signature));
    }

    public static Result<byte[]> SignIfPe(byte[] data, X509Certificate2 certificate)
    {
        return PeFile.IsPeFile(data)
            ? Sign(data, certificate)
            : Result.Success(data);
    }
}
