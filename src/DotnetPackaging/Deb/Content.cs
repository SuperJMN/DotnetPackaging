namespace DotnetPackaging.Deb;

public class Content
{
    public Content(Func<IObservable<byte>> bytes)
    {
        Bytes = bytes;
    }

    public Func<IObservable<byte>> Bytes { get; }
    public bool IsExecutable { get; set; } = false;
}