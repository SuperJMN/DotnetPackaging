using Zafiro.DivineBytes;

namespace DotnetPackaging.Publish;

public sealed record PublishResult(RootContainer Container, Maybe<string> Name, string OutputDirectory, IDisposable? Cleanup = null) : IDisposable
{
    public void Dispose()
    {
        Cleanup?.Dispose();
    }
}