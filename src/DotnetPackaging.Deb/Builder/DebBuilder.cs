using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Builder;

public class DebBuilder
{
    public FromContainerOptions Container(IContainer root, string? name = null)
    {
        return new FromContainerOptions(root, Maybe<string>.From(name));
    }
}
