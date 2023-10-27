using CSharpFunctionalExtensions;

namespace Archiver;

public interface ITarFile
{
    Task AddFileEntry(string key, Stream stream);
    Task AddDirectoryEntry(string key, Stream stream);
    Task<Result> Build(Stream target);
}