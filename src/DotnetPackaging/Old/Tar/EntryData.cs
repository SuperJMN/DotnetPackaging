namespace DotnetPackaging.Old.Tar;

public record EntryData(string Name, Properties Properties, Func<IObservable<byte>> Contents);