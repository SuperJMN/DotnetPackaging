using CSharpFunctionalExtensions;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.Exe.Installer.Core;

public sealed class DefaultInstallerPayload : IInstallerPayload
{
    private readonly object gate = new();
    private InstallerPayload? cached;

    public Task<Result<InstallerMetadata>> GetMetadata(CancellationToken ct = default)
        => EnsureLoaded(ct).Map(p => p.Metadata);

    public Task<Result<long>> GetContentSize(CancellationToken ct = default)
        => EnsureLoaded(ct).Map(p => p.ContentSizeBytes);

    public Task<Result> CopyContents(string targetDirectory, IObserver<Progress>? progressObserver = null, CancellationToken ct = default)
        => EnsureLoaded(ct).Bind(p => Task.Run(() =>
            PayloadExtractor.CopyContentTo(p, targetDirectory, progressObserver), ct));

    private Task<Result<InstallerPayload>> EnsureLoaded(CancellationToken ct)
        => Task.Run(() =>
        {
            lock (gate)
            {
                if (cached is not null) return Result.Success(cached);
                var res = PayloadExtractor.LoadPayload();
                if (res.IsSuccess)
                {
                    cached = res.Value;
                }
                return res;
            }
        }, ct);
}