using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage;

internal class ApplicationDirectory : IZafiroDirectory
{
    private readonly Application application;

    public ApplicationDirectory(Application application)
    {
        this.application = application;
    }

    public Task<Result> Create() => throw new NotImplementedException();

    public Task<Result<IEnumerable<IZafiroFile>>> GetFiles() => throw new NotImplementedException();

    public Task<Result<IEnumerable<IZafiroDirectory>>> GetDirectories()
    {
        var applicationContents = application.Contents;

        var zafiroDirectories = new List<IZafiroDirectory>()
        {
            applicationContents,
        };

        var success = Result.Success<IEnumerable<IZafiroDirectory>>(zafiroDirectories);
        return Task.FromResult(success);
    }

    public Task<Result> Delete() => throw new NotImplementedException();

    public ZafiroPath Path { get; }
    public Task<Result<bool>> Exists { get; }
    public IFileSystemRoot FileSystem { get; }
    public Task<Result<DirectoryProperties>> Properties { get; }
    public IObservable<FileSystemChange> Changed { get; }
}