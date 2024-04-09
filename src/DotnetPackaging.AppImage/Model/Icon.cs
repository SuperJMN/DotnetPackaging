using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Model;

public class Icon : IIcon
{
    public Icon(IGetStream streamFactory)
    {
        StreamFactory = streamFactory.StreamFactory;
    }

    public Func<Task<Result<Stream>>> StreamFactory { get; }
}