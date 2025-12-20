namespace DotnetPackaging.Msix.Core.Compression;

internal static class StreamExtensions
{
    // Método de extensión que envuelve la lectura asíncrona sin el sufijo "Async"
    public static Task<int> Leer(this Stream stream, byte[] buffer, int offset, int count) =>
        stream.ReadAsync(buffer, offset, count);
}
