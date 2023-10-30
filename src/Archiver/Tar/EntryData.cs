namespace Archiver.Tar;

public record EntryData(string Name, Properties Properties, Func<Stream> Contents);