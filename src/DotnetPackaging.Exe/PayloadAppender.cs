using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

internal static class PayloadAppender
{
    private const string Magic = "DPACKEXE1";

    public static async Task<Result<IByteSource>> Append(IByteSource stub, IByteSource payload)
    {
        try
        {
            var stubBytes = await ToBytes(stub);
            var payloadBytes = await ToBytes(payload);
            var lengthBytes = BitConverter.GetBytes((long)payloadBytes.Length);
            var magicBytes = Encoding.ASCII.GetBytes(Magic);

            await using var output = new MemoryStream();
            await output.WriteAsync(stubBytes);
            await output.WriteAsync(payloadBytes);
            await output.WriteAsync(lengthBytes);
            await output.WriteAsync(magicBytes);

            return Result.Success((IByteSource)ByteSource.FromBytes(output.ToArray()));
        }
        catch (Exception ex)
        {
            return Result.Failure<IByteSource>(ex.Message);
        }
    }

    private static async Task<byte[]> ToBytes(IByteSource source)
    {
        await using var stream = source.ToStreamSeekable();
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
