using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public sealed class MaterializedByteSourceFile : IDisposable
{
    private bool disposed;

    private MaterializedByteSourceFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public long Length => new FileInfo(Path).Length;

    public static MaterializedByteSourceFile Create(string extension = "")
    {
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith('.') ? extension : $".{extension}";

        var path = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(),
            $"dotnetpackaging-{Guid.NewGuid():N}{normalizedExtension}");

        return new MaterializedByteSourceFile(path);
    }

    public Stream OpenRead()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public IByteSource ToByteSource()
    {
        return ByteSource.FromStreamFactory(OpenRead, Maybe.From(Length));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch
        {
        }
    }
}
