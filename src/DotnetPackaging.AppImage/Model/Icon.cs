using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Model;

public class Icon : IIcon
{
    public Icon(Func<Task<Result<Stream>>> streamFactory)
    {
        StreamFactory = streamFactory;
    }

    public Func<Task<Result<Stream>>> StreamFactory { get; }
}