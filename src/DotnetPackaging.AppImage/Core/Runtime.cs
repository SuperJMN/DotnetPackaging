using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

internal class Runtime(IByteSource source, Architecture architecture) : IRuntime
{
    public IByteSource Source { get; } = source;
    public Architecture Architecture { get; } = architecture;

    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return Source.Subscribe(observer);
    }

    public IObservable<byte[]> Bytes => Source;
}
