using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage;

public class CraftedDirectory : IZafiroDirectory
{
    private readonly Func<CraftedDirectory, IEnumerable<IZafiroFile>> files;
    private readonly IEnumerable<IZafiroDirectory> directories;

    public CraftedDirectory(Func<IZafiroDirectory, IEnumerable<IZafiroFile>> files, IEnumerable<IZafiroDirectory> directories)
    {
        this.files = files;
        this.directories = directories;
    }

    public Task<Result> Create() => throw new NotImplementedException();

    public Task<Result<IEnumerable<IZafiroFile>>> GetFiles() => Task.FromResult(Result.Success(files(this)));

    public Task<Result<IEnumerable<IZafiroDirectory>>> GetDirectories() => Task.FromResult(Result.Success(directories));

    public Task<Result> Delete() => throw new NotImplementedException();

    public ZafiroPath Path => ZafiroPath.Empty;
    public Task<Result<bool>> Exists { get; }
    public IFileSystemRoot FileSystem { get; }
    public Task<Result<DirectoryProperties>> Properties { get; }
    public IObservable<FileSystemChange> Changed { get; }
}