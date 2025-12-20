using DotnetPackaging.Deb.Builder;

namespace DotnetPackaging.Deb;

internal static class DebFile
{
    public static DebBuilder From() => new();
}
