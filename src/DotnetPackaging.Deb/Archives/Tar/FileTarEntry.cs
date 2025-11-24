namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry(string Path, byte[] Content, TarFileProperties Properties) : TarEntry(Path, Properties);
