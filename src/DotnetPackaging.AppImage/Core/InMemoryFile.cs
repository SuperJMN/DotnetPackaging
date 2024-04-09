using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

internal class InMemoryFile : IZafiroFile
{
    public string Name { get; }
    public IZafiroDirectory Parent { get; }
    public IGetStream GetStream { get; }

    public InMemoryFile(string name, IZafiroDirectory parent, IGetStream getStream)
    {
        Name = name;
        Parent = parent;
        GetStream = getStream;
    }

    public Task<Result> Delete() => throw new NotImplementedException();

    public Task<Result> SetContents(IObservable<byte> contents, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

    public Task<Result<Stream>> GetData() => throw new NotImplementedException();

    public Task<Result> SetData(Stream stream, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

    public IObservable<byte> Contents { get; }
    public Task<Result<bool>> Exists { get; }
    public ZafiroPath Path { get; }
    public Task<Result<FileProperties>> Properties { get; }
    public Task<Result<IDictionary<HashMethod, byte[]>>> Hashes { get; }
    public IFileSystemRoot FileSystem { get; }
}