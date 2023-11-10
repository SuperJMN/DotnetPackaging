namespace DotnetPackaging.Tar;

public record ByteFlow(IObservable<byte> Origin, long Length);