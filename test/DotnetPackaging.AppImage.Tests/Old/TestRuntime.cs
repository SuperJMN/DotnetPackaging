using DotnetPackaging.AppImage.Core;
using Zafiro.Mixins;

namespace DotnetPackaging.AppImage.Tests;

public class TestRuntime : IRuntime
{
    public Task<Result<Stream>> Open() => Task.FromResult(Result.Success("Stub runtime!".PadRight(1024).ToStream()));
}