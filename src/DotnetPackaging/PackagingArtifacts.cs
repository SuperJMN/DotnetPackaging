using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public record PackagingArtifacts(IObservable<Result<INamedByteSource>> Packages, IReadOnlyCollection<IDisposable> Disposables)
{
    public static PackagingArtifacts FromPackage(INamedByteSource package, params IDisposable[] disposables)
    {
        return FromResult(Result.Success(package), disposables);
    }

    public static PackagingArtifacts FromResult(Result<INamedByteSource> package, params IDisposable[] disposables)
    {
        return new PackagingArtifacts(Observable.Return(package), (disposables ?? Array.Empty<IDisposable>()).ToArray());
    }

    public static PackagingArtifacts FromPackages(IEnumerable<Result<INamedByteSource>> packages, params IDisposable[] disposables)
    {
        var safePackages = packages ?? Enumerable.Empty<Result<INamedByteSource>>();
        return new PackagingArtifacts(safePackages.ToObservable(), (disposables ?? Array.Empty<IDisposable>()).ToArray());
    }
}
