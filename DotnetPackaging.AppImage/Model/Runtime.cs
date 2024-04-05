using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Model;

public class Runtime : IGetStream
{
    public Func<Task<Result<Stream>>> StreamFactory => throw new NotImplementedException();
}