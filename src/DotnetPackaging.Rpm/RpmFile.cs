using DotnetPackaging.Rpm.Builder;

namespace DotnetPackaging.Rpm;

public static class RpmFile
{
    public static RpmBuilder From() => new();
}
