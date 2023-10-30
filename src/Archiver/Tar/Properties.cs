namespace Archiver.Tar;

public class Properties
{
    public Properties(DateTimeOffset lastModification)
    {
        LastModification = lastModification;
    }

    public DateTimeOffset LastModification { get; }
}