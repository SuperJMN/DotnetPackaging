using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Builder;

internal class DebBuilder
{
    public FromContainerOptions Container(IContainer root, string? name = null)
    {
        return new FromContainerOptions(root, Maybe<string>.From(name));
    }
}
