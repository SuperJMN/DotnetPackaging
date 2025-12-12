using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public record PackagingArtifacts(IObservable<INamedByteSource> Resources, IReadOnlyCollection<IDisposable> Disposables)
{
    public static PackagingArtifacts FromPackage(INamedByteSource package, params IDisposable[] disposables)
    {
        return FromPackages(new[] { package }, disposables);
    }

    public static PackagingArtifacts FromPackages(IEnumerable<INamedByteSource> packages, params IDisposable[] disposables)
    {
        var safeResources = packages ?? Enumerable.Empty<INamedByteSource>();
        return new PackagingArtifacts(safeResources.ToObservable(), (disposables ?? Array.Empty<IDisposable>()).ToArray());
    }
}
