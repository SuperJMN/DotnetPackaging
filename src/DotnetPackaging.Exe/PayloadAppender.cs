using System.Text;

namespace DotnetPackaging.Exe;

public static class PayloadAppender
{
    public static void AppendPayload(string signedStubPath, string payloadZipPath, string outputPath)
    {
        var stubBytes = File.ReadAllBytes(signedStubPath);
        var payloadBytes = File.ReadAllBytes(payloadZipPath);
        var lengthBytes = BitConverter.GetBytes((long)payloadBytes.Length);
        var magicBytes = Encoding.ASCII.GetBytes("DPACKEXE1");

        using var output = File.Create(outputPath);
        output.Write(stubBytes);
        output.Write(payloadBytes);
        output.Write(lengthBytes);
        output.Write(magicBytes);
    }
}
