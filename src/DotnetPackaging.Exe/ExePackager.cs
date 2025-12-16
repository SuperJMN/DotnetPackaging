using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using Serilog;
using System.Net.Http;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

/// <summary>
/// High-level API for creating Windows EXE installers from .NET projects.
/// </summary>
public static class ExePackager
{
    /// <summary>
    /// Creates a lazy IByteSource that publishes the project and packages it as an EXE installer on-demand.
    /// </summary>
    public static IByteSource FromProject(
        string projectPath,
        Action<ExeOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        var options = new ExeOptions();
        configure?.Invoke(options);

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var effectiveLogger = logger ?? Log.Logger;
        var publisher = new DotnetPublisher(Maybe<ILogger>.From(effectiveLogger));
        var factory = httpClientFactory ?? new SimpleHttpClientFactory();
        var stubProvider = new InstallerStubProvider(effectiveLogger, factory, publisher);
        var exeService = new ExePackagingService(publisher, stubProvider, effectiveLogger);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            async container =>
            {
                var ridResult = GetRid(publishRequest.Rid.GetValueOrDefault("win-x64"));
                if (ridResult.IsFailure)
                {
                    return Result.Failure<IByteSource>(ridResult.Error);
                }

                var setupOptions = new Options();
                if (options.Name != null) setupOptions.Name = Maybe<string>.From(options.Name);
                if (options.Version != null) setupOptions.Version = Maybe<string>.From(options.Version);
                if (options.Comment != null) setupOptions.Comment = Maybe<string>.From(options.Comment);
                if (options.Id != null) setupOptions.Id = Maybe<string>.From(options.Id);

                var result = await exeService.BuildFromDirectory(
                    container,
                    System.IO.Path.GetFileNameWithoutExtension(projectPath) + ".exe",
                    setupOptions,
                    options.Vendor,
                    ridResult.Value,
                    options.Stub,
                    options.SetupLogo,
                    Maybe<string>.From(System.IO.Path.GetFileNameWithoutExtension(projectPath)),
                    options.ProjectMetadata);

                return result.Map(package => (IByteSource)package);
            });
    }

    /// <summary>
    /// Publishes the project, packages it as an EXE installer, and writes to the output path.
    /// </summary>
    public static async Task<Result> PackProject(
        string projectPath,
        string outputPath,
        Action<ExeOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        var source = FromProject(projectPath, configure, publishConfigure, logger, httpClientFactory);
        return await source.WriteTo(outputPath);
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
public class ExeOptions
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
