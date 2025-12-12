using Zafiro.DivineBytes;

namespace DotnetPackaging;

public interface IResourceSession : IDisposable
{
    IObservable<INamedByteSource> Resources { get; }
}
