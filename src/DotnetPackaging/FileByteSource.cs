using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class FileByteSource
{
    public static IByteSource OpenRead(FileInfo file)
    {
        return ByteSource.FromStreamFactory(file.OpenRead).WithLength(file.Length);
    }

    public static IByteSource OpenRead(string path)
    {
        return OpenRead(new FileInfo(path));
    }
}
