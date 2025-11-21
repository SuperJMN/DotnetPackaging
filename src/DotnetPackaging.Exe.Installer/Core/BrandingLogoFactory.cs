using Avalonia.Media.Imaging;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class BrandingLogoFactory
{
    public static IBitmap? FromBytes(IByteSource? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = bytes.ToStreamSeekable();
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
