namespace DotnetPackaging.Msix.Core.Compression;

internal class Block
{
    /// <summary>
    /// Tamaño original del bloque antes de la compresión
    /// </summary>
    public byte[] UncompressedData { get; set; }

    /// <summary>
    /// Datos comprimidos
    /// </summary>
    public byte[] CompressedData { get; set; }

    /// <summary>
    /// Posición del bloque en la secuencia original
    /// </summary>
    public long BlockPosition { get; set; }
}
