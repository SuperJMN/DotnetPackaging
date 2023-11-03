namespace DotnetPackaging.Deb;

public abstract class Content
{
    public Content(Func<IObservable<byte>> bytes)
    {
        Bytes = bytes;
    }

    public Func<IObservable<byte>> Bytes { get; }
}

public class RegularContent : Content
{
    public RegularContent(Func<IObservable<byte>> bytes) : base(bytes)
    {
    }
}

public class ExecutableContent : Content
{
    public ExecutableContent(Func<IObservable<byte>> bytes, IconResources icons) : base(bytes)
    {
        Icons = icons;
    }

    public IconResources Icons { get; }
}