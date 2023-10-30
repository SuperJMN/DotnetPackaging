namespace Archiver.Tar;

public record EntryData(string Name, Properties Properties, Stream Contents);