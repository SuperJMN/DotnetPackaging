using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class Runtime : IStreamOpen
{
    public Func<Task<Result<Stream>>> Open => throw new NotImplementedException();
}