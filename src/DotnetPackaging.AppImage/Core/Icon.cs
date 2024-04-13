using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class Icon : IIcon
{
    public Icon(IStreamOpen streamOpenFactory)
    {
        Open = streamOpenFactory.Open;
    }

    public Func<Task<Result<Stream>>> Open { get; }
}