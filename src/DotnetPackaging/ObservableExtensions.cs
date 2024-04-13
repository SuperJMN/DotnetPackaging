using System.Reactive.Linq;

namespace DotnetPackaging;

public static class ObservableExtensions
{
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