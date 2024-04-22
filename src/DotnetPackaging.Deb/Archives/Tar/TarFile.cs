namespace DotnetPackaging.Deb.Archives.Tar;

public record TarFile(params TarEntry[] Entries);