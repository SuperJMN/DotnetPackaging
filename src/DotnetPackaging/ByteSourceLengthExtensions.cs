using System.Reflection;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class ByteSourceLengthExtensions
{
    public static Maybe<long> KnownLength(this IByteSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is IKnownLengthByteSource known)
        {
            return known.Length;
        }

        var property = source.GetType().GetProperty("Length", BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
        {
            return Maybe<long>.None;
        }

        return property.GetValue(source) switch
        {
            Maybe<long> length => length,
            long length => Maybe.From(length),
            int length => Maybe.From((long)length),
            _ => Maybe<long>.None
        };
    }

    public static IByteSource WithLength(this IByteSource source, long length)
    {
        return source.WithLength(Maybe.From(length));
    }

    public static IByteSource WithLength(this IByteSource source, Maybe<long> length)
    {
        return length.HasValue
            ? new KnownLengthByteSource(source, length)
            : source;
    }

    public static IByteSource ConcatWithLength(params IByteSource[] sources)
    {
        return ConcatWithLength((IEnumerable<IByteSource>)sources);
    }

    public static IByteSource ConcatWithLength(this IEnumerable<IByteSource> sources)
    {
        var sourceList = sources.ToList();
        if (sourceList.Count == 0)
        {
            return ByteSource.FromBytes([]).WithLength(0);
        }

        var bytes = sourceList.Select(source => source.Bytes).Concat();
        var length = sourceList.All(source => source.KnownLength().HasValue)
            ? Maybe.From(sourceList.Sum(source => source.KnownLength().Value))
            : Maybe<long>.None;

        return ByteSource.FromByteObservable(bytes).WithLength(length);
    }

    private interface IKnownLengthByteSource : IByteSource
    {
        Maybe<long> Length { get; }
    }

    private sealed class KnownLengthByteSource(IByteSource source, Maybe<long> length) : IKnownLengthByteSource
    {
        public IObservable<byte[]> Bytes => source.Bytes;
        public Maybe<long> Length => length;
        public IDisposable Subscribe(IObserver<byte[]> observer) => source.Subscribe(observer);
    }
}
