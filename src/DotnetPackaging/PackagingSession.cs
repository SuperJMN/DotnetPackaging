using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public class PackagingSession : IPackagingSession
{
    private readonly CompositeDisposable disposables;

    public PackagingSession(IObservable<Result<INamedByteSource>> packages, IEnumerable<IDisposable> disposables)
    {
        Packages = packages ?? throw new ArgumentNullException(nameof(packages));
        this.disposables = new CompositeDisposable((disposables ?? Enumerable.Empty<IDisposable>()).ToArray());
    }

    public IObservable<Result<INamedByteSource>> Packages { get; }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
