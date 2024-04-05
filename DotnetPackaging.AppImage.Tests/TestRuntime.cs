using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.Mixins;

namespace DotnetPackaging.AppImage.Tests;

public class TestRuntime : IRuntime
{
    public Func<Task<Result<Stream>>> StreamFactory => () => Task.FromResult(Result.Success("Stub runtime".ToStream()));
}