using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public interface IRuntime : IByteSource
{
    Architecture Architecture { get; }
}