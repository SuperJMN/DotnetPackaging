using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class ElfInspector
{
    private const int EtExec = 2;
    private const int EtDyn = 3;
    
    public static Result<Architecture> GetArchitecture(this Stream stream)
    {
        using var br = new BinaryReader(stream);
        var magicNumber = br.ReadBytes(4);
        if (magicNumber[0] != 0x7f || magicNumber[1] != 'E' || magicNumber[2] != 'L' || magicNumber[3] != 'F')
        {
            return Result.Failure<Architecture>("Not an ELF file");
        }

        var fileClass = br.ReadByte(); // 1 = ELF32, 2 = ELF64
        var dataEncoding = br.ReadByte(); // 0 = inválido, 1 = Little Endian, 2 = Big Endian

        stream.Seek(fileClass == 2 ? 20 : 18, SeekOrigin.Begin); // Ajusta la posición según si el archivo es de 32 o 64 bits

        var architecture = br.ReadUInt16();
        if (dataEncoding == 2) // Si el archivo es Big Endian, invertimos los bytes
        {
            architecture = (ushort)((architecture >> 8) | (architecture << 8));
        }

        // Aquí manejamos el caso especial para los archivos ELF generados por .NET para linux-x64
        if (fileClass == 2 && architecture == 1) // archivo de 64 bits con identificador x86 de 32 bits
        {
            return Architecture.X64; // Asumimos que es x86_64
        }

        return architecture switch
        {
            0x03 => Architecture.X86,
            0x3E => Architecture.X64,
            0x28 => Architecture.Arm,
            0xB7 => Architecture.Arm64,
            _ => Result.Failure<Architecture>("Unknown architecture")
        };
    }

    public static Result<bool> IsElf(this Stream stream)
    {
        using var reader = new BinaryReader(stream);
        var magicBytes = new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' };
        var fileMagicBytes = reader.ReadBytes(4);
                
        if (!magicBytes.SequenceEqual(fileMagicBytes))
        {
            return false; // No es un archivo ELF.
        }

        // Saltar a la posición 16 para leer e_type (teniendo en cuenta ELF 32 y 64 bits)
        stream.Seek(16, SeekOrigin.Begin);
        var eType = reader.ReadUInt16(); // e_type es un campo de 2 bytes.

        var isExecutable = eType == EtExec || eType == EtDyn;
        return isExecutable; // Verdadero si el archivo es un ejecutable ELF.
    }
}