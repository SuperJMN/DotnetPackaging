using System.Reactive.Linq;

namespace DotnetPackaging;

public static class ZafiroMixin
{
    public static IObservable<T> Flatten<T>(this IObservable<IObservable<T>> enumerable)
    {
        return enumerable.SelectMany(x => x);
    }
    
    public static IObservable<T> Flatten<T>(this IObservable<IEnumerable<T>> enumerable)
    {
        return enumerable.SelectMany(x => x);
    }
    
    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> enumerable)
    {
        return enumerable.SelectMany(x => x);
    }
}