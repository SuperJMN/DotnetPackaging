using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core;

namespace DotnetPackaging.Msix;

public class Msix
{
    public static Result<IByteSource> FromDirectory(IDirectory directory, Maybe<ILogger> logger)
    {
        return new MsixPackager(logger).Pack(directory);
    }
}