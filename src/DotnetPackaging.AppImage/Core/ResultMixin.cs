using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class ResultMixin
{
    public static async Task<Result<IEnumerable<TResult>>> Combine<T, TResult>(this IEnumerable<T> enumerable, Func<T, Task<Result<TResult>>> selector)
    {
        var whenAll = await Task.WhenAll(enumerable.Select(selector));
        return whenAll.Combine();
    }

    public static Task<Result<(T, Q)>> Combine<T, Q>(
        this Task<Result<T>> one,
        Task<Result<Q>> another)
    {
        return one.Bind(x => another.Map(y => (x, y)));
    }

    public static Result Check(this Result result, Func<Result> func)
    {
        return result.Bind(func);
    }
}