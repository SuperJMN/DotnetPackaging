using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class ElfInspector
{
    public static Result<Architecture> GetArchitecture(this Stream stream)
    {
        using var fs = stream;
        using var br = new BinaryReader(fs);
        var magicNumber = br.ReadBytes(4);
        if (magicNumber[0] != 0x7f || magicNumber[1] != 'E' || magicNumber[2] != 'L' || magicNumber[3] != 'F')
        {
            return Result.Failure<Architecture>("Not an ELF file");
        }
        var fileClass = br.ReadByte();
            
        fs.Seek(18, SeekOrigin.Begin); // Posición para ELF de 32 bits por defecto
        if (fileClass == 2) // Si es de 64 bits
        {
            fs.Seek(20, SeekOrigin.Begin); // Ajustamos la posición para ELF de 64 bits
        }
        var architecture = br.ReadUInt16();

        return architecture switch
        {
            0x03 => Architecture.X86,
            0x3E => Architecture.X64,
            0x28 => Architecture.Arm,
            0xB7 => Architecture.Arm64,
            _ => Result.Failure<Architecture>("Unknown architecture")
        };
    }
    
    private const string ElfMagicNumber = "\x7FELF";

    public static Result<bool> IsElf(this Stream stream)
    {
        using var reader = new BinaryReader(stream);
        var magicNumber = new string(reader.ReadChars(4));
        return magicNumber.Equals(ElfMagicNumber);
    }
}