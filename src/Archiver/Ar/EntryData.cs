namespace Archiver.Ar;

public record EntryData(string Name, Properties Properties, Func<Stream> Contents);