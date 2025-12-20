using DotnetPackaging.Rpm.Builder;

namespace DotnetPackaging.Rpm;

internal static class RpmFile
{
    public static RpmBuilder From() => new();
}
