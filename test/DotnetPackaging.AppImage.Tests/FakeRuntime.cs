namespace DotnetPackaging.AppImage.Tests;

public class FakeRuntime : IRuntime
{
    private readonly ByteArrayData data;

    public FakeRuntime()
    {
        data = new ByteArrayData(new byte[0]);
    }
    
    public IObservable<byte[]> Bytes => data.Bytes;

    public long Length => data.Length;
}