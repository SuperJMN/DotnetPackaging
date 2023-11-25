using System.Reactive.Linq;

namespace DotnetPackaging.Common;

public static class ObservableExtensions
{
    public static IObservable<byte> ConcatPadding(this IObservable<byte> observable, int padding, byte value)
    {
        return observable.Concat(PaddedByteArray(padding, value).ToObservable());
    }

    private static byte[] PaddedByteArray(int size, byte value)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, value);
        return bytes;
    }

    public static IObservable<T> AsBlocks<T>(this IObservable<T> sequence, int blockSize, T paddingItem)
    {
        return sequence
            .Buffer(blockSize)
            .Select(block =>
            {
                int paddingCount = blockSize - block.Count;
                if (paddingCount > 0)
                {
                    var paddingBlock = Enumerable.Range(1, paddingCount).Select(_ => paddingItem);
                    return block.Concat(paddingBlock).ToObservable();
                }
                return block.ToObservable();
            }).Concat();
    }
}