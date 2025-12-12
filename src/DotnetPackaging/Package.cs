using System;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public sealed class Package : IPackage
{
    private readonly IByteSource source;
    private readonly IDisposable? cleanup;

    public Package(string name, IByteSource source, IDisposable? cleanup = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.cleanup = cleanup;
    }

    public string Name { get; }

    public IObservable<byte[]> Bytes => source.Bytes;

    public IDisposable Subscribe(IObserver<byte[]> observer) => source.Subscribe(observer);

    public void Dispose()
    {
        cleanup?.Dispose();
    }
}
