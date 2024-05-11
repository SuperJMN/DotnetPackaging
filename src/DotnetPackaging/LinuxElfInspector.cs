using CSharpFunctionalExtensions;
using System.Reactive.Linq;
using DotnetPackaging;
using Zafiro.FileSystem;
using Zafiro.Reactive;

public static class LinuxElfInspector
{
    private const int EtExec = 2;
    private const int EtDyn = 3;
    private const int HeaderLength = 20; // Mayor longitud necesaria para verificar ELF32 y ELF64

    public static IObservable<Result<Architecture>> GetArchitecture(this IFile file)
    {
        return GetArchitecture(file.Bytes);
    }
    
    public static IObservable<Result<Architecture>> GetArchitecture(IObservable<byte[]> byteChunks)
    {
        var state = new List<byte>();
        var observable = byteChunks
            .Flatten() // Aplana los arrays en bytes individuales
            .Take(HeaderLength) // Solo toma la cantidad de bytes necesaria para determinar la arquitectura
            .ToArray() // Junta los bytes hasta HeaderLength
            .Select(bytes => ParseElfHeader(bytes));

        return observable;
    }

    private static Result<Architecture> ParseElfHeader(byte[] bytes)
    {
        if (bytes.Length < 20)
        {
            return Result.Failure<Architecture>("Incomplete ELF header");
        }

        // Comprobamos el número mágico
        if (bytes[0] != 0x7F || bytes[1] != 'E' || bytes[2] != 'L' || bytes[3] != 'F')
        {
            return Result.Failure<Architecture>("Not an ELF file");
        }

        var fileClass = bytes[4]; // 1 = ELF32, 2 = ELF64
        var dataEncoding = bytes[5]; // 0 = inválido, 1 = Little Endian, 2 = Big Endian

        int architectureOffset = fileClass == 2 ? 18 : 16;
        var architecture = BitConverter.ToUInt16(bytes, architectureOffset);
        if (dataEncoding == 2) // Si es Big Endian, invertimos los bytes
        {
            architecture = (ushort)((architecture >> 8) | (architecture << 8));
        }

        // Caso especial para archivos ELF .NET linux-x64
        if (fileClass == 2 && architecture == 1)
        {
            return Architecture.X64; // x86_64 asumido
        }

        return architecture switch
        {
            0x03 => Architecture.X86,
            0x3E => Architecture.X64,
            0x28 => Architecture.Arm32,
            0xB7 => Architecture.Arm64,
            _ => Result.Failure<Architecture>("Unknown architecture")
        };
    }

    public static IObservable<Result<bool>> IsElf(this IFile file)
    {
        return IsElf(file.Bytes);
    }

    public static IObservable<Result<bool>> IsElf(IObservable<byte[]> byteChunks)
    {
        var observable = byteChunks
            .Flatten() // Aplana arrays a bytes individuales
            .Take(18) // Toma bytes suficientes para verificar ELF
            .ToArray() // Junta todos los bytes en un array
            .Select(bytes => CheckElfExecutable(bytes));

        return observable;
    }

    private static Result<bool> CheckElfExecutable(byte[] bytes)
    {
        if (bytes.Length < 18)
        {
            return Result.Failure<bool>("Not enough bytes to check ELF header");
        }

        var magicBytes = new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' };
        if (!magicBytes.SequenceEqual(bytes[0..4]))
        {
            return Result.Failure<bool>("Not an ELF file");
        }

        var eType = BitConverter.ToInt16(bytes, 16);
        var isExecutable = eType == EtExec || eType == EtDyn;
        return Result.Success(isExecutable);
    }
}
