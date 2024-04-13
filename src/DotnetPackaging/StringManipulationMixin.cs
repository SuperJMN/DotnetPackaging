using System.Text;

namespace DotnetPackaging;

public static class StringManipulationMixin
{
    public static string Truncate(this string str, int totalWidth) => new(str.Take(totalWidth).ToArray());
    public static string FromCrLfToLf(this string str) => str.Replace("\r\n", "\n");
    public static byte[] GetAsciiBytes(this string str) => Encoding.ASCII.GetBytes(str);

    public static string ToOctal(this int number) => Convert.ToString(number, 8);
    public static string ToOctal(this long number) => Convert.ToString(number, 8);

    public static string ToOctalField(this long number) => Convert.ToString(number, 8).NullTerminatedPaddedField(12);

    public static string PaddedField(this string str, int size) => str.PadLeft(size - 1, '0');

    public static string NullTerminatedPaddedField(this string str, int size) => str.PaddedField(size).NullTerminated();

    public static string NullTerminated(this string str) => str + "\0";
}