using System.Text.Json;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.ProgressReporting;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe.Installer.Core;

internal sealed class MetadataFilePayload : IInstallerPayload
{
    private readonly string directory;
    private readonly string metadataPath;

    private MetadataFilePayload(string metadataPath, string directory)
    {
        this.metadataPath = metadataPath;
        this.directory = directory;
    }

    public static Result<MetadataFilePayload> FromProcessDirectory()
    {
        if (Environment.ProcessPath is not { } processPath)
        {
            return Result.Failure<MetadataFilePayload>("Process path is unavailable.");
        }

        var directory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Result.Failure<MetadataFilePayload>("Process directory cannot be determined.");
        }

        return FromDirectory(directory);
    }

    public static Result<MetadataFilePayload> FromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Result.Failure<MetadataFilePayload>("Directory cannot be null or whitespace.");
        }

        var metadataPath = Path.Combine(directory, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return Result.Failure<MetadataFilePayload>($"metadata.json not found in {directory}");
        }

        return Result.Success(new MetadataFilePayload(metadataPath, directory));
    }

    public Task<Result<InstallerMetadata>> GetMetadata(CancellationToken ct = default)
    {
        return Task.FromResult(Result.Try(() =>
        {
            var json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<InstallerMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return metadata ?? throw new InvalidOperationException("metadata.json is invalid.");
        }, ex => $"Failed to read metadata: {ex.Message}"));
    }

    public Task<Result<long>> GetContentSize(CancellationToken ct = default)
    {
        return Task.FromResult(Result.Failure<long>("Disk-only payload does not provide installation content."));
    }

    public Task<Result<Maybe<IByteSource>>> GetLogo(CancellationToken ct = default)
    {
        return Task.FromResult(Result.Try(() =>
        {
            var logoPath = Path.Combine(directory, "logo.png");
            if (!File.Exists(logoPath))
            {
                return Maybe<IByteSource>.None;
            }

            var bytes = File.ReadAllBytes(logoPath);
            return Maybe<IByteSource>.From(ByteSource.FromBytes(bytes));
        }, ex => $"Failed to read logo: {ex.Message}"));
    }

    public Task<Result> CopyContents(string targetDirectory, IObserver<Progress>? progressObserver = null, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Failure("Disk-only payload does not provide installation content."));
    }

    public Task<Result<Maybe<string>>> MaterializeUninstaller(string targetDirectory, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success(Maybe<string>.None));
    }
}
