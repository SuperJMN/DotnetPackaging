using Zafiro.DivineBytes;

namespace DotnetPackaging;

public interface IPackagingSession : IDisposable
{
    IObservable<Result<INamedByteSource>> Packages { get; }
}