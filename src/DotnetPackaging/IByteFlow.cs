namespace DotnetPackaging;

public interface IByteFlow
{
    IObservable<byte> Bytes { get; }
    long Length { get; }
}