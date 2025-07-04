using System.Text;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using Directory = Zafiro.DivineBytes.Directory;
using File = Zafiro.DivineBytes.File;

namespace DotnetPackaging.AppImage.Tests2;

public class CreateAppImage
{
    [Fact]
    public void Create()
    {
        var runtime = new Runtime(ByteSource.FromString("THIS IS MY RUNTIME", Encoding.UTF8));
        var file = new File("Hola", ByteSource.FromString("Hola Mundo", Encoding.UTF8));
        
        IEnumerable<IDirectory> directories = new List<IDirectory>();
        IEnumerable<File> files = [file];
        var dir = new Directory("", files, directories);
        var unixDir = dir.ToUnixDirectory(new DefaultMetadataResolver());
        var sut = new WIP.AppImage(runtime, unixDir);
    }
}

public class DefaultMetadataResolver : IMetadataResolver
{
    public Metadata ResolveDirectory(IDirectory dir)
    {
        return new Metadata(Permission.All, 0);
    }

    public Metadata ResolveFile(INamedByteSource file)
    {
        return new Metadata(Permission.All, 0);
    }
}

public class Runtime(IByteSource source) : IRuntime
{
    public IByteSource Source { get; } = source;

    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return Source.Subscribe(observer);
    }

    public IObservable<byte[]> Bytes => Source;
}