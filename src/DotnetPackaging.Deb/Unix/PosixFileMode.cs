namespace DotnetPackaging.Deb.Unix;

public readonly record struct PosixFileMode(int Value)
{
    public static PosixFileMode Parse(string octalValue)
    {
        var parsed = Convert.ToInt32(octalValue, 8);
        return new PosixFileMode(parsed);
    }
}

public static class PosixFileModeExtensions
{
    public static PosixFileMode ToFileMode(this string value)
    {
        return PosixFileMode.Parse(value);
    }

    public static string ToFileModeString(this PosixFileMode mode)
    {
        return Convert.ToString(mode.Value, 8).PadLeft(5, '0');
    }
}
