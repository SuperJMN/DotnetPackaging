namespace DotnetPackaging.Tar;

public record EntryData(string Name, Properties Properties, Func<IObservable<byte>> Contents, FiniteByteSequence ByteSequence = null);