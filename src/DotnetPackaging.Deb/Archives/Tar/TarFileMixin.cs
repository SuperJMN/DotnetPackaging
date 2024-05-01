using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarFileMixin
{
    public static IByteProvider ToByteProvider(this TarFile tarFile)
    {
        var entries = tarFile.Entries.Select(tarEntry =>
        {
            return tarEntry switch
            {
                DirectoryTarEntry dirTarEntry => dirTarEntry.ToByteProvider(),
                FileTarEntry fileTarEntry => fileTarEntry.ToByteProvider(),
                _ => throw new NotSupportedException()
            };
        });

        var entriesProvider = new CompositeByteProvider(entries.ToArray());
        return entriesProvider;
        //return entriesProvider.PadToNearestMultiple(2 * 512);
    }
}