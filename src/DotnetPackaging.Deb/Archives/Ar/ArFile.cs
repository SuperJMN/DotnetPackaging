using DotnetPackaging.Deb.Bytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public record ArFile(params Entry[] Entries);

public record Entry(string Name, IData Content, Properties Properties);
