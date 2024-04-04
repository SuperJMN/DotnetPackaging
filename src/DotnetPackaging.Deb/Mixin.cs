using System.Text;

namespace DotnetPackaging.Deb;

public static class Mixin
{
    public static byte[] GetAsciiBytes(this string content) => Encoding.ASCII.GetBytes(content);    
}