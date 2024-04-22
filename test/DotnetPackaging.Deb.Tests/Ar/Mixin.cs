using System.Text;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Tests.Ar;

public static class Mixin
{
    public static Task<Result<T>> Success<T>(T value)
    {
        return Task.FromResult(Result.Success(value));
    }
    
    public static Func<Task<Result<T>>> SuccessFunc<T>(T value)
    {
        return () => Task.FromResult(Result.Success(value));
    }

    public static string ToAscii(this IEnumerable<byte> bytes)
    {
        return ToAscii(bytes.ToArray());
    }
    
    public static string ToAscii(this byte[] bytes)
    {
        return Encoding.ASCII.GetString(bytes);
    }
}