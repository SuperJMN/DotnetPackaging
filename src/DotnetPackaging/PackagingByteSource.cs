using System.Reactive.Linq;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class PackagingByteSource
{
    public static IByteSource FromResultFactory(Func<Task<Result<IByteSource>>> factory)
    {
        return ByteSource.FromByteObservable(Observable.Defer(() =>
            Observable.FromAsync(factory)
                .SelectMany(result => result.IsSuccess
                    ? result.Value.Bytes
                    : Observable.Throw<byte[]>(new InvalidOperationException(result.Error)))));
    }

    public static IByteSource FromFailure(string error) =>
        FromResultFactory(() => Task.FromResult(Result.Failure<IByteSource>(error)));
}
