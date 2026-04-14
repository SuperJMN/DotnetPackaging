using System.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core.Manifest;
using DotnetPackaging.Msix.Core.Signing;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Msix;

/// <summary>
/// MSIX packager that produces Store-ready packages with optional signing and visual asset generation.
/// </summary>
public sealed class MsixPackager
{
    /// <summary>
    /// Creates an MSIX package from a container with full Store-ready options.
    /// </summary>
    /// <param name="container">Source container with application files.</param>
    /// <param name="metadata">Optional manifest metadata. If provided, AppxManifest.xml is generated.</param>
    /// <param name="signingOptions">Optional signing configuration.</param>
    /// <param name="sourceIcon">Optional source icon bytes for visual asset generation.</param>
    /// <param name="logger">Optional logger.</param>
    public Task<Result<IByteSource>> Pack(
        IContainer container,
        Maybe<AppManifestMetadata> metadata,
        Maybe<SigningOptions> signingOptions = default,
        Maybe<byte[]> sourceIcon = default,
        ILogger? logger = null)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));

        var log = Maybe<ILogger>.From(logger);
        var certificateResult = ResolveCertificate(signingOptions, metadata);

        if (certificateResult.IsFailure)
            return Task.FromResult(Result.Failure<IByteSource>(certificateResult.Error));

        var certificate = certificateResult.Value;

        var result = metadata.HasValue
            ? Msix.FromDirectoryAndMetadata(container, metadata.Value, sourceIcon, certificate, log)
            : Msix.FromDirectory(container, certificate, log);

        return Task.FromResult(result);
    }

    private static Result<Maybe<X509Certificate2>> ResolveCertificate(
        Maybe<SigningOptions> signingOptions,
        Maybe<AppManifestMetadata> metadata)
    {
        if (!signingOptions.HasValue)
            return Result.Success(Maybe<X509Certificate2>.None);

        var opts = signingOptions.Value;
        return CertificateProvider
            .Get(opts.PfxPath, opts.PfxPassword, opts.PublisherCN ?? metadata.Map(m => m.Publisher).GetValueOrDefault("CN=Publisher"))
            .Map(cert => Maybe<X509Certificate2>.From(cert));
    }
}

/// <summary>
/// Options for signing an MSIX package.
/// </summary>
public class SigningOptions
{
    /// <summary>Optional path to a PFX certificate file. If omitted, a self-signed certificate is generated.</summary>
    public Maybe<string> PfxPath { get; set; }

    /// <summary>Password for the PFX file.</summary>
    public Maybe<string> PfxPassword { get; set; }

    /// <summary>Publisher CN for self-signed certificate generation. Falls back to the manifest Publisher if not set.</summary>
    public string? PublisherCN { get; set; }
}
