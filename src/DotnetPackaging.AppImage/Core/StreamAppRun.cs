using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class StreamAppRun(IGetStream streamFactory) : IAppRun
{
    public Func<Task<Result<Stream>>> StreamFactory { get; } = streamFactory.StreamFactory;
}