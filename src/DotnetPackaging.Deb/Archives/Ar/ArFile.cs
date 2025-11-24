using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public record ArFile(params ArEntry[] Entries);

public record ArEntry(string Name, IByteSource Content, UnixFileProperties Properties);
