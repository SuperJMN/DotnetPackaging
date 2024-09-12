
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Deb.Archives.Ar;

public record ArFile(params Entry[] Entries);

public record Entry(IFile File, Properties Properties);