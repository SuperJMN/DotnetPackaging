using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Archives.Deb;

public class ByteProviderFile : IFile
{
    private readonly IByteProvider byteProvider;

    public ByteProviderFile(string name, IByteProvider byteProvider)
    {
        this.byteProvider = byteProvider;
        Name = name;
    }
    
    public string Name { get; }
    public IObservable<byte[]> Bytes => byteProvider.Bytes;
    public long Length => byteProvider.Length;
}

