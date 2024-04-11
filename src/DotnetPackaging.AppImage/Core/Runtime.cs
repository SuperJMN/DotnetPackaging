using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class Runtime : IGetStream
{
    public Func<Task<Result<Stream>>> StreamFactory => throw new NotImplementedException();
}