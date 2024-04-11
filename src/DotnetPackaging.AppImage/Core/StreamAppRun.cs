using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class StreamAppRun(IStreamOpen streamOpenFactory) : IAppRun
{
    public Func<Task<Result<Stream>>> Open { get; } = streamOpenFactory.Open;
}