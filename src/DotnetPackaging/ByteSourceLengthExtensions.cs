using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class ByteSourceLengthExtensions
{
    /// <summary>
    /// Returns length metadata already carried by the byte source.
    /// </summary>
    /// <remarks>
    /// This method must stay metadata-only. Never compute length by subscribing, blocking, calling ReadAll,
    /// or writing the source to memory/disk; callers that require a stream length must decide explicitly
    /// whether unknown length justifies materialization.
    /// </remarks>
    public static Maybe<long> KnownLength(this IByteSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Length;
    }

    public static IByteSource WithLength(this IByteSource source, long length)
    {
        return source.WithLength(Maybe.From(length));
    }

    public static IByteSource WithLength(this IByteSource source, Maybe<long> length)
    {
        ArgumentNullException.ThrowIfNull(source);
        return length.HasValue ? ByteSource.FromByteObservable(source.Bytes, length) : source;
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
            return ByteSource.FromBytes([]);
        }

        return ByteSource.Concat(sourceList);
    }
}
