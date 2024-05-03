using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Archives.Deb;

public class ByteProviderFile : IFile
{
    private readonly IData data;

    public ByteProviderFile(string name, IData data)
    {
        this.data = data;
        Name = name;
    }
    
    public string Name { get; }
    public IObservable<byte[]> Bytes => data.Bytes;
    public long Length => data.Length;
}

