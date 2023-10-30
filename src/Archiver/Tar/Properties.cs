namespace Archiver.Tar;

public class Properties
{
    public Properties(DateTimeOffset lastModification, FileModes fileModes)
    {
        LastModification = lastModification;
        FileModes = fileModes;
    }

    public DateTimeOffset LastModification { get; }
    public FileModes FileModes { get; }
}