using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Tests;

public static class Mixin
{
    public static Task<Result> ToFile(this Stream stream, IZafiroFile file)
    {
        return file.SetData(stream);
    }
        
    public static Result<IEnumerable<TResult>> MapMany<TInput, TResult>(
        this Result<IEnumerable<TInput>> taskResult,
        Func<TInput, TResult> selector)
    {
        return taskResult.Map((Func<IEnumerable<TInput>, IEnumerable<TResult>>)(inputs => inputs.Select<TInput, TResult>(selector)));
    }

    public static async Task<Result<IEnumerable<TResult>>> MapAndCombine<TInput, TResult>(
        this Result<IEnumerable<Task<Result<TInput>>>> enumerableOfTaskResults,
        Func<TInput, TResult> selector)
    {
        var result = await enumerableOfTaskResults.Map(async taskResults =>
        {
            var p = await Task.WhenAll(taskResults).ConfigureAwait(false);
            return p.Select(x => x.Map(selector)).Combine();
        }).ConfigureAwait(false);

        return result;
    }
}