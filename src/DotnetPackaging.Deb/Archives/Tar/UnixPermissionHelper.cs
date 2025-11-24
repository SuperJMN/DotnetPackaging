using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class UnixPermissionHelper
{
    public static UnixPermissions FromOctal(string octal)
    {
        var owner = ParseDigit(octal[0]);
        var group = ParseDigit(octal[1]);
        var other = ParseDigit(octal[2]);

        return new UnixPermissions(
            owner.read, owner.write, owner.execute,
            group.read, group.write, group.execute,
            other.read, other.write, other.execute);
    }

    private static (bool read, bool write, bool execute) ParseDigit(char digit)
    {
        var value = digit - '0';
        return (
            (value & 4) == 4,
            (value & 2) == 2,
            (value & 1) == 1);
    }
}
