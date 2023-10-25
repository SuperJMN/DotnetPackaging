using CSharpFunctionalExtensions;

namespace Archiver;

public class FileEntry
{
    public string Name { get; }
    public Stream Stream { get; }
    public DateTimeOffset DateTimeOffset { get; }

    private FileEntry(string name, Stream stream, DateTimeOffset dateTimeOffset)
    {
        Name = name;
        Stream = stream;
        DateTimeOffset = dateTimeOffset;
    }

    public static Result<FileEntry> Create(string name, Stream stream, DateTimeOffset dateTimeOffset)
    {
        return Result.Success(new FileEntry(name, stream, dateTimeOffset));
    }
}