using DotnetPackaging.NewTar;

namespace DotnetPackaging.Common;

public record ByteFlow(IObservable<byte> Bytes, long Length) : IByteFlow;