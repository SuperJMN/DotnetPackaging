using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public class PackagingSession : IResourceSession
{
    private readonly CompositeDisposable disposables;

    public PackagingSession(IObservable<INamedByteSource> resources, IEnumerable<IDisposable> disposables)
    {
        Resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.disposables = new CompositeDisposable((disposables ?? Enumerable.Empty<IDisposable>()).ToArray());
    }

    public IObservable<INamedByteSource> Resources { get; }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
