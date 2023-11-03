namespace DotnetPackaging.Tests.Tar;

public static class TestMixin
{
    public static void DumpTo(this IEnumerable<byte> bytes, string path)
    {
        using var stream = File.Create(path);
        stream.Write(bytes.ToArray());
    }
}