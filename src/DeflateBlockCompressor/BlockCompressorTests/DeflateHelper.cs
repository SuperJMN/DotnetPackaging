using System.IO.Compression;
namespace BlockCompressorTests;

public static class DeflateHelper
{
    /// <summary>
    /// Decompresses a byte array that has been compressed with the Deflate algorithm.
    /// </summary>
    /// <param name="compressedData">Array of compressed bytes.</param>
    /// <returns>Array of decompressed bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if compressedData is null.</exception>
    /// <exception cref="InvalidDataException">Thrown if the compressed data is corrupted or has an incorrect format.</exception>
    public static byte[] DecompressDeflateData(byte[] compressedData)
    {
        if (compressedData == null)
            throw new ArgumentNullException(nameof(compressedData));
        using (var compressedStream = new MemoryStream(compressedData))
        using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            deflateStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }
    /// <summary>
    /// Decompresses a byte array that has been compressed with the Deflate algorithm and returns the result as a UTF-8 text string.
    /// </summary>
    /// <param name="compressedData">Array of compressed bytes.</param>
    /// <returns>Decompressed string in UTF-8.</returns>
    /// <exception cref="ArgumentNullException">Thrown if compressedData is null.</exception>
    /// <exception cref="InvalidDataException">Thrown if the compressed data is corrupted or has an incorrect format.</exception>
    public static string DecompressDeflateDataToString(byte[] compressedData)
    {
        byte[] decompressedData = DecompressDeflateData(compressedData);
        return System.Text.Encoding.UTF8.GetString(decompressedData);
    }
    /// <summary>
    /// Usage example to decompress a file that has been compressed with Deflate.
    /// </summary>
    /// <param name="compressedFilePath">Path of the compressed file.</param>
    /// <param name="decompressedFilePath">Path where the decompressed file will be saved.</param>
    public static void DecompressDeflateFile(string compressedFilePath, string decompressedFilePath)
    {
        using (var fileStream = File.OpenRead(compressedFilePath))
        using (var deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
        using (var outputStream = File.Create(decompressedFilePath))
        {
            deflateStream.CopyTo(outputStream);
        }
    }
}