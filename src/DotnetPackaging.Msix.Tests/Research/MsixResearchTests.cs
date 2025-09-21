using System.IO.Compression;
using System.IO.Hashing;
using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;

namespace MsixPackaging.Tests.Research;

public class MsixResearchTests
{
    [Fact]
    public void Block_hashes()
    {
        var compressedBytes = Tools.ExtractRawCompressedBytes(
            @"F:\Repos\SuperJMN\MsixCreator\MsixPackaging.Tests\bin\Debug\net9.0\TestFiles\ValidExe\Expected.msix",
            "HelloWorld.exe");

        var validBlock = compressedBytes.Skip(1).Take(65560).ToArray();
        var hash = Convert.ToBase64String(SHA256.HashData(validBlock));
        Assert.Equal("j/tch6hTj5ey+gF7s81GyCj0/KLi3TQu8FwnCmJrRS4=", hash);
    }

    [Fact]
    public void Calculate_correct_hash()
    {
        var compressedBytes = Tools.ExtractRawCompressedBytes(
            @"F:\Repos\SuperJMN\MsixCreator\MsixPackaging.Tests\bin\Debug\net9.0\TestFiles\ValidExe\Expected.msix", "HelloWorld.exe");

        // 1. El bloque está completamente separado del LFH (Local File Header)
        // No necesitamos hacer Skip(44) ya que los bytes ya son solo del bloque

        // 2. Los dos primeros bytes (00 09) son probablemente un marcador de bloque Deflate
        // En el formato Deflate, estos pueden indicar nivel de compresión o flags

        // 3. Necesitamos normalizar estos bytes según las especificaciones de MSIX
        byte[] normalizedBytes = new byte[65560];
        Array.Copy(compressedBytes, 2, normalizedBytes, 0, 65560);

        // Al normalizar los bytes, estamos tomando exactamente los 65560 bytes
        // excluyendo los dos primeros bytes (00 09) que son metadatos

        string hash = Convert.ToBase64String(SHA256.HashData(normalizedBytes));
        Console.WriteLine($"Hash calculado: {hash}");
        Assert.Equal("j/tch6hTj5ey+gF7s81GyCj0/KLi3TQu8FwnCmJrRS4=", hash);
    }

    [Fact]
    public void Try_appx_specific_hash_calculation()
    {
        var compressedBytes = Tools.ExtractRawCompressedBytes(
            @"F:\Repos\SuperJMN\MsixCreator\MsixPackaging.Tests\bin\Debug\net9.0\TestFiles\ValidExe\Expected.msix", "HelloWorld.exe");

        // En el formato ZIP/APPX, hay posibilidad de que ciertos bits necesiten ser normalizados
        // Los primeros bytes de un bloque Deflate incluyen flags de compresión
        byte[] normalizedBytes = compressedBytes.ToArray();

        // 1. Normalizar flags del encabezado Deflate
        normalizedBytes[0] = 0x78; // Valor estándar para Deflate (ZLIB)
        normalizedBytes[1] = 0x9C; // Nivel de compresión predeterminado

        string hash = Convert.ToBase64String(SHA256.HashData(normalizedBytes.Take(65560).ToArray()));
        Console.WriteLine($"Hash calculado con flags normalizados: {hash}");
        Assert.Equal("j/tch6hTj5ey+gF7s81GyCj0/KLi3TQu8FwnCmJrRS4=", hash);
    }

    [Fact]
    public void Patcher()
    {
        var compressedBytes = Tools.ExtractRawCompressedBytes(
            @"F:\Repos\SuperJMN\MsixCreator\MsixPackaging.Tests\bin\Debug\net9.0\TestFiles\ValidExe\Expected.msix", "HelloWorld.exe");

        byte[] testBytes = compressedBytes.ToArray();
        // Conservar bits importantes y limpiar los demás
        testBytes[0] &= 0xF0; // Conservar los 4 bits más significativos
        testBytes[1] &= 0xF0; // Conservar los 4 bits más significativos
        var hash = Convert.ToBase64String(SHA256.HashData(testBytes.Take(65560).ToArray()));
        Assert.Equal("j/tch6hTj5ey+gF7s81GyCj0/KLi3TQu8FwnCmJrRS4=", hash);
    }
}