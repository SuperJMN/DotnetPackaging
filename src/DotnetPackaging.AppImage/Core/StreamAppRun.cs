using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage.Core;

public class StreamAppRun(Func<Task<Result<Stream>>> streamFactory) : IAppRun
{
    public Func<Task<Result<Stream>>> StreamFactory { get; } = streamFactory;
}