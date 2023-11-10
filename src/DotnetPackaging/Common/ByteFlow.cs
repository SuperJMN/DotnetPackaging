using DotnetPackaging.NewTar;

namespace DotnetPackaging.Common;

public record ByteFlow(IObservable<byte> Origin, long Length) : IByteFlow;