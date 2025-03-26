using CSharpFunctionalExtensions;
using MsixPackaging.Core;
using Zafiro.DivineBytes;

namespace MsixPackaging;

public class Msix
{
    public static Result<IByteSource> FromDirectory(IDirectory directory, Maybe<ILogger> logger)
    {
        return new MsixPackager(logger).Pack(directory);
    }
}