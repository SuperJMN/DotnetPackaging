using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.Exe.Installer.Core;

public interface IInstallerPayload
{
    Task<Result<InstallerMetadata>> GetMetadata(CancellationToken ct = default);

    Task<Result<long>> GetContentSize(CancellationToken ct = default);

    Task<Result<Maybe<IByteSource>>> GetLogo(CancellationToken ct = default);

    Task<Result> CopyContents(
        string targetDirectory,
        IObserver<Progress>? progressObserver = null,
        CancellationToken ct = default);

    Task<Result<Maybe<string>>> MaterializeUninstaller(
        string targetDirectory,
        CancellationToken ct = default);
}