using System.Reactive.Linq;
using System.Xml.Linq;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Ar;

public record ArFile(params Entry[] Entries);

public record Entry(IFile File, Properties Properties);