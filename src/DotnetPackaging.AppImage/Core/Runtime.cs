using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public class Runtime(IByteSource source) : IRuntime
{
    public IByteSource Source { get; } = source;

    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return Source.Subscribe(observer);
    }

    public IObservable<byte[]> Bytes => Source;
}