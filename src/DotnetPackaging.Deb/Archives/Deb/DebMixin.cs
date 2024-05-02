using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Archives.Deb;

public class ByteProviderFile : IFile
{
    private readonly IObservableDataStream observableDataStream;

    public ByteProviderFile(string name, IObservableDataStream observableDataStream)
    {
        this.observableDataStream = observableDataStream;
        Name = name;
    }
    
    public string Name { get; }
    public IObservable<byte[]> Bytes => observableDataStream.Bytes;
    public long Length => observableDataStream.Length;
}

