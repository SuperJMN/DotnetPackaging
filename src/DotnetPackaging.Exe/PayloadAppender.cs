using System.Text;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

internal static class PayloadAppender
{
    private const string Magic = "DPACKEXE1";

    public static Task<Result<IByteSource>> Append(IByteSource stub, IByteSource payload)
    {
        var payloadLength = payload.KnownLength();
        if (payloadLength.HasValue)
        {
            return Task.FromResult(Result.Success<IByteSource>(
                new[] { stub, payload, Footer(payloadLength.Value) }.ConcatWithLength()));
        }

        return Task.FromResult(Result.Success<IByteSource>(ByteSource.FromDisposableAsync(
            () => payload.ToTempFile(".exe-payload"),
            payloadFile =>
            {
                var footer = Footer(payloadFile.Length);
                return new[] { stub, payloadFile.ToByteSource(), footer }.ConcatWithLength();
            })));
    }

    private static IByteSource Footer(long payloadLength)
    {
        var lengthBytes = BitConverter.GetBytes(payloadLength);
        var magicBytes = Encoding.ASCII.GetBytes(Magic);
        var footerBytes = lengthBytes.Concat(magicBytes).ToArray();
        return ByteSource.FromBytes(footerBytes).WithLength(footerBytes.LongLength);
    }
}
