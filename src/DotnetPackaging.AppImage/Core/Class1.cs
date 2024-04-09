using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class ElfExecutableChecker
{
    private const string ElfMagicNumber = "\x7FELF";

    public static Result<bool> IsLinuxExecutable(this Stream stream)
    {
        using (var reader = new BinaryReader(stream))
        {
            var magicNumber = new string(reader.ReadChars(4));
            return magicNumber.Equals(ElfMagicNumber);
        }
    }
}