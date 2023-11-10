namespace DotnetPackaging.Tar;

public record FiniteByteSequence(IObservable<byte> Contents, long Length);