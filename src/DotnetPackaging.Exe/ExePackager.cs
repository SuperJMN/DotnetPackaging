using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using System.Net.Http;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

/// <summary>
/// Windows EXE packager.
/// </summary>
public sealed class ExePackager
{
    private readonly DotnetPublisher publisher;
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;

    public ExePackager(DotnetPublisher? publisher = null, ILogger? logger = null, IHttpClientFactory? httpClientFactory = null)
    {
        this.logger = logger ?? Log.Logger;
        this.publisher = publisher ?? new DotnetPublisher(Maybe<ILogger>.From(this.logger));
        this.httpClientFactory = httpClientFactory ?? new SimpleHttpClientFactory();
    }

    /// <summary>
    /// Creates an EXE installer from a container and metadata.
    /// </summary>
    public Task<Result<IByteSource>> Pack(IContainer container, ExePackagerMetadata metadata)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var rid = metadata.RuntimeIdentifier.GetValueOrDefault("win-x64");
        return GetRid(rid).Bind(async validRid =>
        {
            var outputName = metadata.OutputName.GetValueOrDefault("setup.exe");
            var options = metadata.Options;
            var vendor = metadata.Vendor.GetValueOrDefault(null);
            var stub = metadata.Stub.GetValueOrDefault(null);
            var setupLogo = metadata.SetupLogo.GetValueOrDefault(null);

            var stubProvider = new InstallerStubProvider(logger, httpClientFactory, publisher);
            var exeService = new ExePackagingService(publisher, stubProvider, logger);

            var result = await exeService.BuildFromDirectory(
                container,
                outputName,
                options,
                vendor,
                validRid,
                stub,
                setupLogo,
                metadata.ProjectName,
                metadata.ProjectMetadata);

            return result.Map(package => (IByteSource)package);
        });
    }

    private static Result<string> GetRid(string rid)
    {
        var valid = new[] { "win-x64", "win-arm64", "win-x86" };
        return valid.Contains(rid, StringComparer.OrdinalIgnoreCase)
            ? Result.Success(rid)
            : Result.Failure<string>($"Invalid Windows RID: {rid}. Use win-x64, win-arm64, or win-x86.");
    }

    private class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
}

/// <summary>
/// Options for EXE installer packaging.
/// </summary>
internal class ExeOptions
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Comment { get; set; }
    public string? Id { get; set; }
    public string? Vendor { get; set; }
    public IByteSource? Stub { get; set; }
    public IByteSource? SetupLogo { get; set; }
    public Maybe<ProjectMetadata> ProjectMetadata { get; set; } = Maybe<ProjectMetadata>.None;
}

public sealed class ExePackagerMetadata
{
    public Options Options { get; set; } = new();
    public Maybe<string> OutputName { get; set; } = Maybe<string>.None;
    public Maybe<string> ProjectName { get; set; } = Maybe<string>.None;
    public Maybe<string> Vendor { get; set; } = Maybe<string>.None;
    public Maybe<string> RuntimeIdentifier { get; set; } = Maybe<string>.None;
    public Maybe<IByteSource> Stub { get; set; } = Maybe<IByteSource>.None;
    public Maybe<IByteSource> SetupLogo { get; set; } = Maybe<IByteSource>.None;
    public Maybe<ProjectMetadata> ProjectMetadata { get; set; } = Maybe<ProjectMetadata>.None;
}
