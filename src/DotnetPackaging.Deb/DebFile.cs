using DotnetPackaging.Deb.Builder;

namespace DotnetPackaging.Deb;

public static class DebFile
{
    public static DebBuilder From() => new();
}