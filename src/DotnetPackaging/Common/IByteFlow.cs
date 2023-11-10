namespace DotnetPackaging.Common;

public interface IByteFlow
{
    IObservable<byte> Bytes { get; }
    long Length { get; }
}