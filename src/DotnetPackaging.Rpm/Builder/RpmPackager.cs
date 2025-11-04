using CSharpFunctionalExtensions;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmPackager
{
    public static Task<Result<RpmPackage>> CreatePackage(PackageMetadata metadata, IReadOnlyList<RpmEntry> entries)
    {
        if (entries.Count == 0)
        {
            return Task.FromResult(Result.Failure<RpmPackage>("The RPM layout does not contain any entries."));
        }

        return Task.FromResult(Result.Success(new RpmPackage(metadata, entries)));
    }
}
