using ClassLibrary1;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Model;

public class Icon : IIcon
{
    public Func<Task<Result<Stream>>> StreamFactory => throw new NotImplementedException();
}

public interface IIcon : IGetStream
{
}