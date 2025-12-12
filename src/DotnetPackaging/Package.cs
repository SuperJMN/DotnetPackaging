using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public sealed class Package : IPackage
{
    private readonly IByteSource source;
    private readonly CompositeDisposable disposables;

    public Package(string name, IByteSource source, IEnumerable<IDisposable>? disposables = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.disposables = new CompositeDisposable((disposables ?? Enumerable.Empty<IDisposable>()).ToArray());
    }

    public string Name { get; }

    public IObservable<byte[]> Bytes => source.Bytes;

    public IDisposable Subscribe(IObserver<byte[]> observer) => source.Subscribe(observer);

    public void Dispose()
    {
        disposables.Dispose();
    }
}
