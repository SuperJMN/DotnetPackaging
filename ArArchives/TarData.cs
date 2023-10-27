using Archiver;
using CSharpFunctionalExtensions;

namespace Archive.Tests;

public class TarData
{
    private readonly IByteWriter writer;

    public TarData(IByteWriter writer)
    {
        this.writer = writer;
    }

    public Result Build()
    {
        Filename("control");
        FileMode();
        Owner();
        Group();
        FileSize();
        LastModification();
        ChecksumPlaceholder();
        LinkIndicator();
        NameOfLinkedFile();
        return Result.Success();
    }

    private void LinkIndicator()
    {
    }

    private void NameOfLinkedFile()
    {
    }

    private void ChecksumPlaceholder()
    {
    }

    private void LastModification()
    {
    }

    private void FileSize()
    {
    }

    private void Group()
    {
        writer.WriteAllBytes(new byte[] { 0x20, 0x20, 0x20, 0x20, 0x37, 0x37, 0x37, 0x0 }, "Group"); 
    }

    private void FileMode()
    {
        writer.WriteAllBytes(new byte[] { 0x20, 0x20, 0x20, 0x20, 0x37, 0x37, 0x37, 0x0 }, "File Mode"); //File mode
    }

    private void Owner()
    {
        writer.WriteAllBytes(new byte[]
        {
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00,
        }, "Owner");
    }

    private void Filename(string filename)
    {
        writer.WriteString(filename.ToFixed(100), "Filename"); 
    }
}