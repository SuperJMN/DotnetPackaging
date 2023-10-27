namespace Archiver;

public static class StringManipulationMixin
{
    public static string ToFixed(this string str, int totalWidth) => str.Truncate(totalWidth).PadRight(totalWidth, '\0');
    public static string Truncate(this string str, int totalWidth) => new(str.Take(totalWidth).ToArray());
}