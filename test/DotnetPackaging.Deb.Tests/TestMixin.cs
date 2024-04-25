using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Mixins;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests;

public static class TestMixin
{
    public static Task<Result<T>> Success<T>(T value)
    {
        return Task.FromResult(Result.Success(value));
    }
    
    public static Func<Task<Result<T>>> SuccessFunc<T>(T value)
    {
        return () => Task.FromResult(Result.Success(value));
    }
    
    public static Func<Task<Result<Stream>>> String(string value)
    {
        return () => Task.FromResult(Result.Success(value.ToStream()));
    }

    public static string ToAscii(this IEnumerable<byte> bytes)
    {
        return ToAscii(bytes.ToArray());
    }
    
    public static string ToAscii(this byte[] bytes)
    {
        return Encoding.ASCII.GetString(bytes);
    }

    public static IFile StringFile(string name, string contents)
    {
        return (IFile)new File(name, String(contents));
    }

    public static string ToAscii(this MemoryStream outputStream) => outputStream.ToArray().ToAscii();
}