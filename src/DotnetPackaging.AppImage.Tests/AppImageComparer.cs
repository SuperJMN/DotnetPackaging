using System.Reactive.Linq;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageStreamComparer
{
    public async Task<bool> AreEqual(Func<IObservable<byte>> first, Func<IObservable<byte>> second)
    {
        var superBlockIndex = await GetSuperBlockId(first());
        var indicesToIgnore = Enumerable.Range(superBlockIndex + 8, 4).ToArray();
        var comparer = new IgnorePositionsComparer<byte>(indicesToIgnore);
        var firstIndexed = first().Indexed();
        var secondIndexed = second().Indexed();
        var sequenceEqual = firstIndexed.SequenceEqual(secondIndexed, comparer);
        var firstBytes = await firstIndexed.ToList();
        var secondBytes = await secondIndexed.ToList();
        var areEqual = await sequenceEqual;
        return areEqual;
    }

    private async Task<int> GetSuperBlockId(IObservable<byte> bytes)
    {
        var firstBlockIndex = await bytes
            .Select((x, i) => (Byte: x, Index: i)) // Mantenemos el byte y su índice original
            .Buffer(32, 1)
            // Proyectamos el bloque a un posible índice si el bloque cumple la condición
            .Select(block => CheckBlock(block.Select(x => x.Byte).ToArray()) ? block.First().Index : (int?) null)
            .FirstOrDefaultAsync(x => x.HasValue); // Buscamos el primer bloque que cumple la condición

        // Devuelve el índice del primer bloque que cumple la condición o -1 si ninguno cumple
        return firstBlockIndex ?? -1;
    }

    // You can define other methods, fields, classes and namespaces here
    private bool CheckBlock(byte[] x)
    {
        if (x.Length < 32)
        {
            return false;
        }

        var magicBytes = x[0] == (byte) 'h' && x[1] == (byte) 's' && x[2] == (byte) 'q' && x[3] == (byte) 's';
        if (magicBytes)
        {
        }

        var version = x[28] == 4 && x[29] == 0 && x[30] == 0 && x[31] == 0;
        return magicBytes && version;
    }

    private class IgnorePositionsComparer<T> : IEqualityComparer<(T, int)>
    {
        private readonly int[] ignoreIndices;

        public IgnorePositionsComparer(int[] ignoreIndices)
        {
            this.ignoreIndices = ignoreIndices;
        }

        public bool Equals((T, int) x, (T, int) y)
        {
            if (ignoreIndices.Contains(y.Item2))
            {
                return true;
            }

            return EqualityComparer<(T, int)>.Default.Equals(x, y);
        }

        public int GetHashCode((T, int) obj) => obj.GetHashCode();
    }
}

internal static class Mixin
{
    public static IObservable<(T, int)> Indexed<T>(this IObservable<T> a)
    {
        return a.Select((x, i) => (x, i));
    }
}