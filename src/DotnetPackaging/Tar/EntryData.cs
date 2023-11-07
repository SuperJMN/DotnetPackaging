using DotnetPackaging.Common;

namespace DotnetPackaging.Tar;

public record EntryData(string Name, Properties Properties, IByteStore Contents);