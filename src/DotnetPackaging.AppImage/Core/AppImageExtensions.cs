using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

internal static class AppImageExtensions
{
    public static Task<Result<IByteSource>> ToByteSource(this AppImageContainer appImageContainer)
    {
        return SquashFS.Create(appImageContainer.Container).Map(squashFs =>
        {
            return new[] { appImageContainer.Runtime, squashFs }.ConcatWithLength();
        });
    }
}
