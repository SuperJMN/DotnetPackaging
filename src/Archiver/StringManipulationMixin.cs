using System.Text;

namespace Archiver;

public static class StringManipulationMixin
{
    public static string ToFixed(this string str, int totalWidth) => str.Truncate(totalWidth).PadRight(totalWidth, '\0');
    public static string Truncate(this string str, int totalWidth) => new(str.Take(totalWidth).ToArray());
    public static string FromCrLfToLf(this string str) => str.Replace("\r\n", "\n");
    public static byte[] GetAsciiBytes(this string str) => Encoding.ASCII.GetBytes(str);

    public static string ToOctalField(this long number)
    {
        return Convert.ToString(number, 8).PadLeft(11, '0') + "\0";
    }
}