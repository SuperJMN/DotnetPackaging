namespace DotnetPackaging;

public record ByteFlow(IObservable<byte> Bytes, long Length) : IByteFlow;