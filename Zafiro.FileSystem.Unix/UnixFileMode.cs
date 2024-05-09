namespace Zafiro.FileSystem.Unix;

[Flags]
public enum UnixFileMode
{
    OwnerRead = 0b100_000_000, // 0400 en octal
    OwnerWrite = 0b010_000_000, // 0200 en octal
    OwnerExecute = 0b001_000_000, // 0100 en octal
    GroupRead = 0b000_100_000, // 0040 en octal
    GroupWrite = 0b000_010_000, // 0020 en octal
    GroupExecute = 0b000_001_000, // 0010 en octal
    OthersRead = 0b000_000_100, // 0004 en octal
    OthersWrite = 0b000_000_010, // 0002 en octal
    OthersExecute = 0b000_000_001, // 0001 en octal
    All = OwnerRead | OwnerWrite | OwnerExecute | GroupRead | GroupWrite | GroupExecute | OthersRead | OthersWrite | OthersExecute
}