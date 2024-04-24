using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public record TarEntry(IFile File, UnixFileProperties Properties)
{
}