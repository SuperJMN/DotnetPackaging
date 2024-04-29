using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarFileMixin
{
    public static IByteProvider ToByteProvider(this TarFile tarFile)
    {
        var byteProviders = tarFile.Entries.Select(tarEntry =>
        {
            return tarEntry switch
            {
                DirectoryTarEntry dirTarEntry => dirTarEntry.ToByteProvider(),
                FileTarEntry fileTarEntry => fileTarEntry.ToByteProvider(),
                _ => throw new NotSupportedException()
            };
        });
        
        return new CompositeByteProvider(byteProviders.ToArray()).PadToNearestMultiple(20 * 512);
    }
}