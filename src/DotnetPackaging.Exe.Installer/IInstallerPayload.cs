using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.Exe.Installer;

public interface IInstallerPayload
{
    Task<Result<InstallerMetadata>> GetMetadata(CancellationToken ct = default);

    Task<Result> CopyContents(
        string targetDirectory,
        IObserver<Progress>? progressObserver = null,
        CancellationToken ct = default);
}